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
using Rehost.WebForms.Parity.Contracts;

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
            var options = ScenarioOptions.Parse(args);
            Run(options);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Run(ScenarioOptions options)
    {
        Environment.SetEnvironmentVariable(TraceChannel.TraceVariable, options.TracePath);

        if (options.Serve)
        {
            ServeAsync(options).GetAwaiter().GetResult();
            return;
        }

        WebFormsApplication.Initialize(new WebFormsApplicationOptions
        {
            ApplicationId = options.ApplicationId,
            PhysicalRootPath = options.ApplicationPath,
            VirtualRootPath = "/",
            CompilationTempDirectory = options.CompilationTempDirectory,
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
            TraceChannel.Record("codegen-dir:" + HttpRuntime.CodegenDir);
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
    private static async Task ServeAsync(ScenarioOptions options)
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
        ScenarioOptions options,
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
        ScenarioOptions options,
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

internal sealed class ScenarioOptions
{
    private ScenarioOptions(
        string applicationId,
        string applicationPath,
        string compilationTempDirectory,
        string tracePath,
        string? holdGate,
        string? responseDirectory,
        string? machineConfigurationPath,
        bool serve,
        long? kestrelMaxBodyBytes,
        bool http2,
        List<string> requests,
        List<string> postbacks)
    {
        KestrelMaxBodyBytes = kestrelMaxBodyBytes;
        Http2 = http2;
        MachineConfigurationPath = machineConfigurationPath;
        Serve = serve;
        HoldGate = holdGate;
        ApplicationId = applicationId;
        ApplicationPath = applicationPath;
        CompilationTempDirectory = compilationTempDirectory;
        TracePath = tracePath;
        ResponseDirectory = responseDirectory;
        Requests = requests;
        Postbacks = postbacks;
    }

    internal string ApplicationId { get; }

    internal string ApplicationPath { get; }

    internal string CompilationTempDirectory { get; }

    internal string TracePath { get; }

    internal string? HoldGate { get; }

    internal string? ResponseDirectory { get; }

    internal string? MachineConfigurationPath { get; }

    internal bool Serve { get; }

    internal long? KestrelMaxBodyBytes { get; }

    internal bool Http2 { get; }

    internal List<string> Requests { get; }

    internal List<string> Postbacks { get; }

    internal static ScenarioOptions Parse(string[] args)
    {
        var applicationId = "scenario";
        string? applicationPath = null;
        string? compilationTempDirectory = null;
        string? tracePath = null;
        string? holdGate = null;
        string? responseDirectory = null;
        string? machineConfigurationPath = null;
        var serve = false;
        long? kestrelMaxBodyBytes = null;
        var http2 = false;
        var requests = new List<string>();
        var postbacks = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--app":
                    applicationPath = Require(value, "--app");
                    i++;
                    break;
                case "--temp":
                    compilationTempDirectory = Require(value, "--temp");
                    i++;
                    break;
                case "--trace":
                    tracePath = Require(value, "--trace");
                    i++;
                    break;
                case "--id":
                    applicationId = Require(value, "--id");
                    i++;
                    break;
                case "--hold-gate":
                    holdGate = Require(value, "--hold-gate");
                    i++;
                    break;
                case "--response-dir":
                    responseDirectory = Path.GetFullPath(Require(value, "--response-dir"));
                    i++;
                    break;
                case "--machine-config":
                    machineConfigurationPath = Path.GetFullPath(Require(value, "--machine-config"));
                    i++;
                    break;
                case "--serve":
                    serve = true;
                    break;
                case "--kestrel-max-body":
                    kestrelMaxBodyBytes = long.Parse(Require(value, "--kestrel-max-body"));
                    i++;
                    break;
                case "--http2":
                    http2 = true;
                    break;
                case "--request":
                    requests.Add(Require(value, "--request"));
                    i++;
                    break;
                case "--postback":
                    postbacks.Add(Require(value, "--postback"));
                    i++;
                    break;
                default:
                    throw new ArgumentException("Unrecognized argument: " + args[i]);
            }
        }

        var ignoredByMode = serve
            ? new (bool Present, string Option)[]
            {
                (machineConfigurationPath != null, "--machine-config"),
                (holdGate != null, "--hold-gate"),
            }
            : [
                (kestrelMaxBodyBytes != null, "--kestrel-max-body"),
                (http2, "--http2"),
                (postbacks.Count != 0, "--postback"),
            ];
        foreach (var (present, option) in ignoredByMode)
        {
            if (present)
            {
                throw new ArgumentException(
                    option + " is not honored in " + (serve ? "--serve" : "batch")
                    + " mode; remove it or switch the mode.");
            }
        }

        // The run mode always issues at least one request; a serve mode with nothing declared is
        // passive and driven by the test's own client.
        if (!serve && requests.Count == 0)
        {
            requests.Add("/default");
        }

        return new ScenarioOptions(
            applicationId,
            Path.GetFullPath(Require(applicationPath, "--app")),
            Path.GetFullPath(Require(compilationTempDirectory, "--temp")),
            Path.GetFullPath(Require(tracePath, "--trace")),
            holdGate,
            responseDirectory,
            machineConfigurationPath,
            serve,
            kestrelMaxBodyBytes,
            http2,
            requests,
            postbacks);
    }

    private static string Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(name + " is required.");
        }

        return value;
    }
}

// ApplicationManager hands the application a registered object, which is the seam through which
// requests enter an activated application.
public sealed class BatchRunner : MarshalByRefObject, IRegisteredObject
{
    public int Request(string path, string? responsePath)
    {
        var request = new ScenarioWorkerRequest(path);
        HttpRuntime.ProcessRequest(request);

        if (!request.WaitForCompletion(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("The request did not complete: " + path);
        }

        if (responsePath != null)
        {
            File.WriteAllBytes(responsePath, request.BodyBytes);
        }

        if (request.StatusCode >= 500)
        {
            TraceChannel.Record(TraceEvents.ErrorBody + Summarize(request.Body));
        }

        return request.StatusCode;
    }

    public void Stop(bool immediate)
    {
        HostingEnvironment.UnregisterObject(this);
    }

    // A compilation error page is thousands of characters of markup; the compiler diagnostics in
    // it are what a failing scenario needs to report.
    private static string Summarize(string body)
    {
        // The style block alone is longer than anything worth recording, and it sits ahead of the
        // compiler diagnostics that make a failing scenario diagnosable.
        var text = Regex.Replace(body, "<(style|script)[^>]*>.*?</\\1>", " ", RegexOptions.Singleline);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text.Length > 600 ? text[..600] : text;
    }
}
