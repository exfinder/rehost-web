using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
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

        WebFormsApplication.Initialize(new WebFormsApplicationOptions
        {
            ApplicationId = options.ApplicationId,
            PhysicalRootPath = options.ApplicationPath,
            VirtualRootPath = "/",
            CompilationTempDirectory = options.CompilationTempDirectory,
            MachineConfigurationFilePath = Path.Combine(
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

            foreach (var path in options.Requests)
            {
                var status = runner.Request(path);
                HostJournal.Record("request:" + path + ":" + status);
            }

            // Holding the process alive keeps its generated assemblies loaded, which is the only
            // way a second process can meet a file it is not allowed to delete.
            if (options.HoldMilliseconds > 0)
            {
                HostJournal.Record("holding");
                Thread.Sleep(options.HoldMilliseconds);
            }
        }
        finally
        {
            manager.StopObject(options.ApplicationId, typeof(ScenarioRunner));
            manager.ShutdownApplication(options.ApplicationId);
            manager.Close();
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
        int holdMilliseconds,
        List<string> requests)
    {
        HoldMilliseconds = holdMilliseconds;
        ApplicationId = applicationId;
        ApplicationPath = applicationPath;
        CompilationTempDirectory = compilationTempDirectory;
        TracePath = tracePath;
        Requests = requests;
    }

    internal string ApplicationId { get; }

    internal string ApplicationPath { get; }

    internal string CompilationTempDirectory { get; }

    internal string TracePath { get; }

    internal int HoldMilliseconds { get; }

    internal List<string> Requests { get; }

    internal static ScenarioOptions Parse(string[] args)
    {
        var applicationId = "scenario";
        string? applicationPath = null;
        string? compilationTempDirectory = null;
        string? tracePath = null;
        var holdMilliseconds = 0;
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
                case "--hold-ms":
                    holdMilliseconds = int.Parse(Require(value, "--hold-ms"), CultureInfo.InvariantCulture);
                    i++;
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
            holdMilliseconds,
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
    public int Request(string path)
    {
        var request = new ScenarioWorkerRequest(path);
        HttpRuntime.ProcessRequest(request);

        if (!request.WaitForCompletion(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("The request did not complete: " + path);
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
