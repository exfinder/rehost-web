using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rehost.WebForms.Hosting;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioHost;

// One application per process is a hard constraint of the runtime, so any test that activates an
// application needs its own process. This host runs one named scenario against one fixture and
// exits, which keeps that constraint in the process boundary instead of in test ordering rules.
//
// It deliberately does not compare against the .NET Framework oracle; that is the parity
// prototype's job. See ADR 0043.
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var scan = ArgumentScan.Scan(args);
            if (scan.Flag(ScenarioHostGrammar.Serve))
            {
                var options = ServeOptions.From(scan);
                Environment.SetEnvironmentVariable(TraceChannel.TraceVariable, options.TracePath);
                ServeAsync(options).GetAwaiter().GetResult();
            }
            else
            {
                var options = BatchOptions.From(scan);
                Environment.SetEnvironmentVariable(TraceChannel.TraceVariable, options.TracePath);
                RunBatch(options);
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void RunBatch(BatchOptions options)
    {
        WebFormsApplication.Initialize(new WebFormsApplicationOptions
        {
            ApplicationId = options.ApplicationId,
            PhysicalRootPath = options.ApplicationPath,
            VirtualRootPath = "/",
            CompilationTempDirectory = options.CompilationTempDirectory,
            MachineKeyDirectory = Path.Combine(
                options.CompilationTempDirectory, ScenarioHostGrammar.MachineKeysDirectoryName),
            MachineConfigurationFilePath = options.MachineConfigurationPath ?? Path.Combine(
                AppContext.BaseDirectory,
                "configs",
                WebFormsApplicationOptions.DefaultMachineConfigurationFileName),
            RootWebConfigurationFilePath = Path.Combine(
                AppContext.BaseDirectory,
                "configs",
                WebFormsApplicationOptions.DefaultRootWebConfigurationFileName),
        });

        var manager = ApplicationManager.GetApplicationManager();
        manager.Open();

        var runner = (BatchRunner)manager.CreateObject(
            options.ApplicationId,
            typeof(BatchRunner),
            "/",
            EnsureTrailingSeparator(options.ApplicationPath),
            failIfExists: true,
            // Mirrors the production adapter: initialization failures surface per request.
            throwOnError: false);

        try
        {
            TraceChannel.Record("private-bytes-limit:" + HttpRuntime.Cache.EffectivePrivateBytesLimit);

            for (var i = 0; i < options.Requests.Count; i++)
            {
                var path = options.Requests[i];
                var responsePath = options.ResponseDirectory == null
                    ? null
                    : Path.Combine(options.ResponseDirectory, i + ".body");

                var status = runner.Request(path, responsePath);
                TraceChannel.Record(TraceEvents.Request + path + ":" + status);
            }

            // Holding the process alive keeps its generated assemblies loaded, which is the only
            // way a second process can meet a file it is not allowed to delete. The caller owns
            // the gate and releases it when it is done, so the hold lasts exactly as long as the
            // overlap being tested rather than a guessed interval.
            if (options.HoldGate != null)
            {
                HoldUntilReleased(options.HoldGate);
            }
        }
        finally
        {
            manager.StopObject(options.ApplicationId, typeof(BatchRunner));
            manager.ShutdownApplication(options.ApplicationId);
            manager.Close();
        }
    }

    // The same application, reached over a socket through the production adapter rather than by
    // calling HttpRuntime.ProcessRequest directly.
    private static async Task ServeAsync(ServeOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            if (options.KestrelMaxBodyBytes is { } maxBody)
            {
                kestrel.Limits.MaxRequestBodySize = maxBody;
            }

            // Cleartext HTTP/2 has no ALPN to choose by, so Kestrel serves h2c only on an
            // endpoint that speaks nothing else.
            if (options.Http2)
            {
                kestrel.ConfigureEndpointDefaults(endpoint =>
                    endpoint.Protocols = HttpProtocols.Http2);
            }
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.AddRehostWebForms(configured =>
        {
            configured.ApplicationId = options.ApplicationId;
            configured.PhysicalRootPath = options.ApplicationPath;
            configured.VirtualRootPath = "/";
            configured.CompilationTempDirectory = options.CompilationTempDirectory;
            configured.MachineKeyDirectory = Path.Combine(
                options.CompilationTempDirectory, ScenarioHostGrammar.MachineKeysDirectoryName);
            configured.MachineConfigurationFilePath = Path.Combine(
                AppContext.BaseDirectory,
                "configs",
                WebFormsApplicationOptions.DefaultMachineConfigurationFileName);
            configured.RootWebConfigurationFilePath = Path.Combine(
                AppContext.BaseDirectory,
                "configs",
                WebFormsApplicationOptions.DefaultRootWebConfigurationFileName);
        });

        var app = builder.Build();
        app.UseRehostWebForms();

        await app.StartAsync();

        try
        {
            var address = app.Urls.FirstOrDefault()
                ?? throw new InvalidOperationException("Kestrel reported no bound address.");

            // Passive mode: the test owns the client; the address line is the handoff, and the
            // host stays up until the test kills it.
            if (options.Postbacks.Count == 0
                && options.Requests.Count == 0)
            {
                TraceChannel.Record(TraceEvents.Address + address);
                await Task.Delay(Timeout.Infinite);
            }

            using var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 1,
                AllowAutoRedirect = false,
            };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            client.Timeout = TimeSpan.FromSeconds(30);

            if (options.Postbacks.Count != 0)
            {
                var index = 0;
                foreach (var probe in options.Postbacks)
                {
                    index = await RunCapturedPostbackAsync(client, options, probe, index);
                }
            }
            else
            {
                for (var i = 0; i < options.Requests.Count; i++)
                {
                    var path = options.Requests[i];
                    using var response = await client.GetAsync(path);
                    await RecordResponseAsync(options, path, response, i);
                }
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    // Replays a postback another runtime rendered, which is what a load-balanced farm spanning
    // both does on every request that lands on the node that did not render the page. The payload
    // ships with the fixture because it is only valid for that page under that key.
    private static async Task<int> RunCapturedPostbackAsync(
        HttpClient client,
        ServeOptions options,
        string probe,
        int index)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in File.ReadAllLines(
            Path.Combine(options.ApplicationPath, "Framework.postback")))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var split = line.IndexOf('=');
            fields[line[..split]] = line[(split + 1)..];
        }

        if (probe == "captured-without-event-validation")
        {
            fields.Remove("__EVENTVALIDATION");
        }

        // The render is not the subject, but it proves the page this payload names still serves,
        // so a failure below cannot be blamed on the fixture being broken.
        string html;
        using (var rendered = await client.GetAsync("/Default.aspx"))
        {
            html = await rendered.Content.ReadAsStringAsync();
            await RecordResponseAsync(options, probe + ":render", rendered, index++);
        }

        using var response = await PostFormAsync(
            client,
            PostbackFormClient.FormAction(html),
            PostbackFormClient.Encode(fields));
        await RecordResponseAsync(options, probe + ":postback", response, index++);

        return index;
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string action,
        string body)
    {
        using var content = new StringContent(body, Encoding.UTF8);
        // A browser sends no charset here, so the default request encoding decides the reading.
        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        return await client.PostAsync(action, content);
    }

    private static async Task RecordResponseAsync(
        ServeOptions options,
        string label,
        HttpResponseMessage response,
        int index)
    {
        var body = await response.Content.ReadAsByteArrayAsync();
        if (options.ResponseDirectory != null)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(options.ResponseDirectory, index + ".body"),
                body);
        }

        TraceChannel.Record(TraceEvents.Request + label + ":" + (int)response.StatusCode);
        TraceChannel.Record(TraceEvents.ContentType + response.Content.Headers.ContentType);
        if ((int)response.StatusCode >= 500)
        {
            var error = Regex.Replace(Encoding.UTF8.GetString(body), @"\s+", " ");
            TraceChannel.Record(
                TraceEvents.ErrorBody + error.Substring(0, Math.Min(1000, error.Length)));
        }
        RecordHeader(response, ProbeHeaders.RemotePort);
        RecordHeader(response, ProbeHeaders.ReadMode);
        RecordHeader(response, ProbeHeaders.Spilled);
    }

    private static void RecordHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values))
        {
            TraceChannel.Record(name.ToLowerInvariant() + ":" + string.Join(",", values));
        }
    }

    private static void HoldUntilReleased(string gateName)
    {
        // A named mutex is the one named synchronization object supported on every platform here;
        // named events and semaphores throw off Windows.
        using var gate = new Mutex(false, gateName);
        var acquired = false;

        TraceChannel.Record("holding");
        try
        {
            acquired = gate.WaitOne(TimeSpan.FromMinutes(2));
        }
        catch (AbandonedMutexException)
        {
            // The caller died holding the gate; releasing this process is the useful response.
            acquired = true;
        }

        if (acquired)
        {
            gate.ReleaseMutex();
        }
    }

    private static string EnsureTrailingSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

}
