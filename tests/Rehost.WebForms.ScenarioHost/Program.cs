using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rehost.WebForms.Hosting;

namespace Rehost.WebForms.ScenarioHost;

// The probe assembly reaches the process only through the fixture's bin directory, so the host
// writes its own entries to the same file rather than referencing it.
internal static class HostJournal
{
    internal const string TraceVariable = "REHOST_SCENARIO_TRACE";

    private static readonly Lock Gate = new();

    internal static void Record(string entry)
    {
        var path = Environment.GetEnvironmentVariable(TraceVariable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        lock (Gate)
        {
            File.AppendAllText(path, entry + Environment.NewLine);
        }
    }
}

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
        Environment.SetEnvironmentVariable(HostJournal.TraceVariable, options.TracePath);

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
                "rehost-webforms.machine.config"),
            RootWebConfigurationFilePath = Path.Combine(
                AppContext.BaseDirectory,
                "configs",
                "rehost-webforms.web.config"),
        });

        var manager = ApplicationManager.GetApplicationManager();
        manager.Open();

        var runner = (ScenarioRunner)manager.CreateObject(
            options.ApplicationId,
            typeof(ScenarioRunner),
            "/",
            EnsureTrailingSeparator(options.ApplicationPath),
            failIfExists: true,
            // Mirrors the production adapter: initialization failures surface per request.
            throwOnError: false);

        try
        {
            HostJournal.Record("codegen-dir:" + HttpRuntime.CodegenDir);
            HostJournal.Record("private-bytes-limit:" + HttpRuntime.Cache.EffectivePrivateBytesLimit);

            for (var i = 0; i < options.Requests.Count; i++)
            {
                var path = options.Requests[i];
                var responsePath = options.ResponseDirectory == null
                    ? null
                    : Path.Combine(options.ResponseDirectory, i + ".body");

                var status = runner.Request(path, responsePath);
                HostJournal.Record("request:" + path + ":" + status);
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
            manager.StopObject(options.ApplicationId, typeof(ScenarioRunner));
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
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);
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
                "rehost-webforms.machine.config");
            configured.RootWebConfigurationFilePath = Path.Combine(
                AppContext.BaseDirectory,
                "configs",
                "rehost-webforms.web.config");
        });

        var app = builder.Build();
        app.UseRehostWebForms();

        await app.StartAsync();

        try
        {
            var address = app.Urls.FirstOrDefault()
                ?? throw new InvalidOperationException("Kestrel reported no bound address.");

            using var client = new HttpClient { BaseAddress = new Uri(address) };
            client.Timeout = TimeSpan.FromSeconds(30);

            for (var i = 0; i < options.Requests.Count; i++)
            {
                var path = options.Requests[i];
                using var response = await client.GetAsync(path);
                var body = await response.Content.ReadAsByteArrayAsync();

                if (options.ResponseDirectory != null)
                {
                    await File.WriteAllBytesAsync(
                        Path.Combine(options.ResponseDirectory, i + ".body"),
                        body);
                }

                HostJournal.Record("request:" + path + ":" + (int)response.StatusCode);
                HostJournal.Record("content-type:" + response.Content.Headers.ContentType);
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static void HoldUntilReleased(string gateName)
    {
        // A named mutex is the one named synchronization object supported on every platform here;
        // named events and semaphores throw off Windows.
        using var gate = new Mutex(false, gateName);
        var acquired = false;

        HostJournal.Record("holding");
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
        List<string> requests)
    {
        MachineConfigurationPath = machineConfigurationPath;
        Serve = serve;
        HoldGate = holdGate;
        ApplicationId = applicationId;
        ApplicationPath = applicationPath;
        CompilationTempDirectory = compilationTempDirectory;
        TracePath = tracePath;
        ResponseDirectory = responseDirectory;
        Requests = requests;
    }

    internal string ApplicationId { get; }

    internal string ApplicationPath { get; }

    internal string CompilationTempDirectory { get; }

    internal string TracePath { get; }

    internal string? HoldGate { get; }

    internal string? ResponseDirectory { get; }

    internal string? MachineConfigurationPath { get; }

    internal bool Serve { get; }

    internal List<string> Requests { get; }

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
        var requests = new List<string>();

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
                case "--request":
                    requests.Add(Require(value, "--request"));
                    i++;
                    break;
                default:
                    throw new ArgumentException("Unrecognized argument: " + args[i]);
            }
        }

        if (requests.Count == 0)
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
            requests);
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
public sealed class ScenarioRunner : MarshalByRefObject, IRegisteredObject
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
            HostJournal.Record("error-body:" + Summarize(request.Body));
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
