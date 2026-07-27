using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Web.Hosting;
using CoreParity.Contracts;
using PortableParity.Runner;
using Rehost.WebForms.Hosting;

namespace PortableParity.Host;

internal static class Program
{
    private const string ApplicationId = "portable-parity:cold-sync";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public static int Main(string[] args)
    {
        using var diagnostics = new RuntimeDiagnosticListener();

        try
        {
            var command = CommandLine.Parse(args);
            var basePath = AppContext.BaseDirectory;
            var applicationPath = command.ApplicationPath
                ?? Path.Combine(basePath, "fixture", "app");

            InPhase("fixture-validation", () => ValidateFixture(applicationPath));
            InPhase(
                "host-registration",
                () => WebFormsApplication.Initialize(new WebFormsApplicationOptions
                {
                    ApplicationId = ApplicationId,
                    PhysicalRootPath = applicationPath,
                    VirtualRootPath = "/",
                    MachineConfigurationFilePath = Path.Combine(
                        basePath,
                        "configs",
                        "rehost-webforms.machine.config"),
                    RootWebConfigurationFilePath = Path.Combine(
                        basePath,
                        "configs",
                        "rehost-webforms.web.config")
                }));

            var trace = RunColdSynchronous(applicationPath);

            if (command.Operation == Operation.Verify)
            {
                InPhase(
                    "verification",
                    () => Verify(
                        command.ExpectedPath
                            ?? Path.Combine(basePath, "oracle", "cold-sync.json"),
                        command.NormalizationPath
                            ?? Path.Combine(basePath, "metadata", "normalization.json"),
                        trace));
            }
            else
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(trace, JsonOptions));
            }

            return 0;
        }
        catch (PhaseException exception)
        {
            WriteDiagnostic(exception.Phase, exception.InnerException!);
            return 1;
        }
        catch (Exception exception)
        {
            WriteDiagnostic("command-line", exception);
            return 1;
        }
    }

    private static PipelineTrace RunColdSynchronous(string applicationPath)
    {
        var manager = InPhase(
            "application-activation",
            ApplicationManager.GetApplicationManager);
        var applicationActivated = false;
        InPhase("application-activation", manager.Open);

        try
        {
            var registered = InPhase(
                "application-activation",
                () => manager.CreateObject(
                    ApplicationId,
                    typeof(PortableRunner),
                    "/",
                    EnsureTrailingDirectorySeparator(Path.GetFullPath(applicationPath)),
                    true,
                    true));
            applicationActivated = true;

            if (registered is not IClassicPipelineRunner runner)
            {
                throw new InvalidOperationException(
                    "ApplicationManager did not return an IClassicPipelineRunner.");
            }

            var request = RequestSpecification.ColdSynchronous();
            var observation = InPhase(
                "request-processing",
                () => runner.Run(request));

            return new PipelineTrace
            {
                SchemaVersion = 1,
                Provenance = new TraceProvenance
                {
                    Oracle = "Rehost WebForms portable runtime",
                    TargetFramework = "net10.0",
                    RuntimeRequirement = ".NET 10",
                    ManagedEntryPoint =
                        "System.Web.HttpRuntime.ProcessRequest(HttpWorkerRequest)",
                    ActivationEntryPoint =
                        "WebFormsApplication.Initialize + ApplicationManager.CreateObject",
                    Fixture = "bodyless-precompiled-handler-v1"
                },
                Scenario = request.Scenario,
                Observation = observation
            };
        }
        finally
        {
            if (applicationActivated)
            {
                InPhase(
                    "application-cleanup",
                    () =>
                    {
                        manager.StopObject(ApplicationId, typeof(PortableRunner));
                        manager.ShutdownApplication(ApplicationId);
                        manager.Close();
                    });
            }
        }
    }

    private static void ValidateFixture(string applicationPath)
    {
        applicationPath = Path.GetFullPath(applicationPath);
        var probePath = Path.Combine(applicationPath, "bin", "CoreParity.Probes.dll");
        var hostProbePath = Path.Combine(
            AppContext.BaseDirectory,
            "CoreParity.Probes.dll");

        if (!File.Exists(Path.Combine(applicationPath, "web.config")))
        {
            throw new FileNotFoundException(
                "Fixture web.config is missing.",
                Path.Combine(applicationPath, "web.config"));
        }

        if (!File.Exists(probePath))
        {
            throw new FileNotFoundException(
                "Probe handler/module assembly must exist only in fixture app/bin.",
                probePath);
        }

        if (File.Exists(hostProbePath))
        {
            throw new InvalidOperationException(
                "Probe assembly must not exist beside the host executable.");
        }

        if (typeof(Program).Assembly.GetReferencedAssemblies().Any(
                name => string.Equals(
                    name.Name,
                    "CoreParity.Probes",
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Host assembly must not reference CoreParity.Probes.");
        }

        if (AppDomain.CurrentDomain.GetAssemblies().Any(
                assembly => string.Equals(
                    assembly.GetName().Name,
                    "CoreParity.Probes",
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Probe assembly was loaded before application activation.");
        }
    }

    private static void Verify(
        string expectedPath,
        string normalizationPath,
        PipelineTrace actual)
    {
        expectedPath = Path.GetFullPath(expectedPath);
        normalizationPath = Path.GetFullPath(normalizationPath);

        ValidateEmptyNormalizationManifest(normalizationPath);

        if (!File.Exists(expectedPath))
        {
            throw new FileNotFoundException(
                "Framework golden trace is absent.",
                expectedPath);
        }

        var expected = JsonSerializer.Deserialize<PipelineTrace>(
            File.ReadAllText(expectedPath),
            JsonOptions)
            ?? throw new InvalidDataException(
                "Framework golden trace deserialized to null.");

        if (expected.SchemaVersion != actual.SchemaVersion)
        {
            throw Mismatch(
                "$.SchemaVersion",
                expected.SchemaVersion,
                actual.SchemaVersion);
        }

        if (!string.Equals(expected.Scenario, actual.Scenario, StringComparison.Ordinal))
        {
            throw Mismatch("$.Scenario", expected.Scenario, actual.Scenario);
        }

        ObservationComparer.Verify(expected.Observation, actual.Observation);
        Console.Error.WriteLine(
            "Portable observation strictly matches the Framework cold-sync golden.");
    }

    private static void ValidateEmptyNormalizationManifest(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Normalization manifest is absent.",
                path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException(
                "Unsupported normalization manifest schema.");
        }

        var rules = root.GetProperty("rules");
        if (rules.ValueKind != JsonValueKind.Array || rules.GetArrayLength() != 0)
        {
            throw new InvalidDataException(
                "The cold-sync normalization manifest must remain empty.");
        }
    }

    private static InvalidOperationException Mismatch(
        string path,
        object? expected,
        object? actual)
    {
        return new InvalidOperationException(
            "Parity mismatch at "
            + path
            + ": expected "
            + JsonSerializer.Serialize(expected, JsonOptions)
            + ", actual "
            + JsonSerializer.Serialize(actual, JsonOptions)
            + ".");
    }

    private static void InPhase(string phase, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            throw new PhaseException(phase, exception);
        }
    }

    private static T InPhase<T>(string phase, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception)
        {
            throw new PhaseException(phase, exception);
        }
    }

    private static void WriteDiagnostic(string phase, Exception exception)
    {
        var diagnostic = new FailureDiagnostic
        {
            SchemaVersion = 1,
            Phase = phase,
            Exception = ExceptionObservation.FromException(exception)
        };
        Console.Error.WriteLine(JsonSerializer.Serialize(diagnostic, JsonOptions));
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private sealed class RuntimeDiagnosticListener : System.Diagnostics.Tracing.EventListener
    {
        protected override void OnEventSourceCreated(
            System.Diagnostics.Tracing.EventSource eventSource)
        {
            if (eventSource.Name == "Rehost.WebForms.Runtime")
            {
                EnableEvents(
                    eventSource,
                    System.Diagnostics.Tracing.EventLevel.Error);
            }
        }

        protected override void OnEventWritten(
            System.Diagnostics.Tracing.EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name != "Rehost.WebForms.Runtime")
            {
                return;
            }

            Console.Error.WriteLine(
                "runtime/"
                + eventData.EventName
                + ": "
                + string.Join(" | ", eventData.Payload ?? (IEnumerable<object?>)Array.Empty<object?>()));
        }
    }

    private sealed class FailureDiagnostic
    {
        public int SchemaVersion { get; set; }

        public string Phase { get; set; } = "";

        public ExceptionObservation Exception { get; set; } = new();
    }

    private sealed class PhaseException : Exception
    {
        internal PhaseException(string phase, Exception innerException)
            : base("Portable parity phase failed: " + phase + ".", innerException)
        {
            Phase = phase;
        }

        internal string Phase { get; }
    }

    private static class ObservationComparer
    {
        internal static void Verify(
            PipelineObservation expected,
            PipelineObservation actual)
        {
            VerifyList(
                "$.Observation.Events",
                expected.Events,
                actual.Events,
                (path, left, right) => VerifyValue(path, left, right));
            VerifyValue(
                "$.Observation.Response.StatusCode",
                expected.Response.StatusCode,
                actual.Response.StatusCode);
            VerifyValue(
                "$.Observation.Response.StatusDescription",
                expected.Response.StatusDescription,
                actual.Response.StatusDescription);
            VerifyList(
                "$.Observation.Response.Headers",
                expected.Response.Headers,
                actual.Response.Headers,
                (path, left, right) =>
                {
                    VerifyValue(path + ".Name", left.Name, right.Name);
                    VerifyValue(path + ".Value", left.Value, right.Value);
                });
            VerifyValue(
                "$.Observation.Response.BodyBase64",
                expected.Response.BodyBase64,
                actual.Response.BodyBase64);
            VerifyList(
                "$.Observation.Response.Flushes",
                expected.Response.Flushes,
                actual.Response.Flushes,
                (path, left, right) => VerifyValue(path, left, right));
            VerifyException(
                "$.Observation.EscapedException",
                expected.EscapedException,
                actual.EscapedException);
            VerifyValue(
                "$.Observation.EndOfRequestCount",
                expected.EndOfRequestCount,
                actual.EndOfRequestCount);
            VerifyValue(
                "$.Observation.CompletionCount",
                expected.CompletionCount,
                actual.CompletionCount);
        }

        private static void VerifyList<T>(
            string path,
            System.Collections.Generic.IReadOnlyList<T> expected,
            System.Collections.Generic.IReadOnlyList<T> actual,
            Action<string, T, T> verifyItem)
        {
            VerifyValue(path + ".Count", expected.Count, actual.Count);

            for (var index = 0; index < expected.Count; index++)
            {
                verifyItem(path + "[" + index + "]", expected[index], actual[index]);
            }
        }

        private static void VerifyException(
            string path,
            ExceptionObservation? expected,
            ExceptionObservation? actual)
        {
            if (expected == null || actual == null)
            {
                if (expected != actual)
                {
                    throw Mismatch(path, expected, actual);
                }

                return;
            }

            VerifyValue(path + ".Type", expected.Type, actual.Type);
            VerifyValue(path + ".Message", expected.Message, actual.Message);
            VerifyValue(path + ".HResult", expected.HResult, actual.HResult);
            VerifyException(path + ".Inner", expected.Inner, actual.Inner);
        }

        private static void VerifyValue<T>(string path, T expected, T actual)
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(
                    expected,
                    actual))
            {
                throw Mismatch(path, expected, actual);
            }
        }
    }
}

internal enum Operation
{
    Run,
    Verify
}

internal sealed class CommandLine
{
    internal Operation Operation { get; private set; }

    internal string? ExpectedPath { get; private set; }

    internal string? NormalizationPath { get; private set; }

    internal string? ApplicationPath { get; private set; }

    internal static CommandLine Parse(string[] args)
    {
        var result = new CommandLine();
        var index = 0;

        if (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
        {
            result.Operation = args[index] switch
            {
                "run" => Operation.Run,
                "verify" => Operation.Verify,
                _ => throw Usage("Unknown command '" + args[index] + "'.")
            };
            index++;
        }

        while (index < args.Length)
        {
            var option = args[index++];

            if (index >= args.Length)
            {
                throw Usage("Missing value for '" + option + "'.");
            }

            var value = args[index++];
            switch (option)
            {
                case "--expected":
                    RequireVerify(result.Operation, option);
                    result.ExpectedPath = value;
                    break;
                case "--normalization":
                    RequireVerify(result.Operation, option);
                    result.NormalizationPath = value;
                    break;
                case "--app":
                    result.ApplicationPath = value;
                    break;
                default:
                    throw Usage("Unknown option '" + option + "'.");
            }
        }

        return result;
    }

    private static void RequireVerify(Operation operation, string option)
    {
        if (operation != Operation.Verify)
        {
            throw Usage("'" + option + "' is valid only for verify.");
        }
    }

    private static ArgumentException Usage(string message)
    {
        return new ArgumentException(
            message
            + Environment.NewLine
            + "Usage:"
            + Environment.NewLine
            + "  dotnet PortableParity.Host.dll run [--app <path>]"
            + Environment.NewLine
            + "  dotnet PortableParity.Host.dll verify [--expected <path>]"
            + " [--normalization <path>] [--app <path>]");
    }
}
