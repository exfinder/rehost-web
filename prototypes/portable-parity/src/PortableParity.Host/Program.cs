using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Hosting;
using CoreParity.Contracts;
using PortableParity.Runner;
using Rehost.WebForms.Hosting;

namespace PortableParity.Host;

internal static class Program
{
    private const int SchemaVersion = 2;
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
            var manifestPath = command.ManifestPath
                ?? Path.Combine(basePath, "metadata", "sessions.json");
            var fixtureRoot = command.FixtureRoot ?? Path.Combine(basePath, "fixture");
            var manifest = InPhase("manifest", () => LoadManifest(manifestPath));

            if (command.Operation == Operation.RunSession)
            {
                var session = manifest.Sessions.FirstOrDefault(
                        candidate => string.Equals(
                            candidate.Name,
                            command.SessionName,
                            StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        "Manifest declares no session named '" + command.SessionName + "'.");

                Console.Out.WriteLine(
                    JsonSerializer.Serialize(RunSession(session, fixtureRoot), JsonOptions));
                return 0;
            }

            var trace = new PipelineTrace
            {
                SchemaVersion = SchemaVersion,
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
                Sessions = manifest.Sessions
                    .Select(session => InPhase(
                        "session:" + session.Name,
                        () => RunSessionProcess(session, manifestPath, fixtureRoot)))
                    .ToList()
            };

            if (command.Operation == Operation.Verify)
            {
                InPhase(
                    "verification",
                    () => Verify(
                        command.ExpectedPath
                            ?? Path.Combine(basePath, "oracle", "sessions.json"),
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

    private static SessionManifest LoadManifest(string path)
    {
        path = Path.GetFullPath(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Session manifest is absent.", path);
        }

        var manifest = JsonSerializer.Deserialize<SessionManifest>(
            File.ReadAllText(path),
            JsonOptions)
            ?? throw new InvalidDataException("Session manifest deserialized to null.");

        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported session manifest schema.");
        }

        if (manifest.Sessions.Count == 0)
        {
            throw new InvalidDataException("Session manifest declares no sessions.");
        }

        return manifest;
    }

    // Each session observes a cold runtime, so it needs a process whose HttpRuntime singleton,
    // AssemblyLoadContext resolver, and activated application have never been touched.
    private static SessionObservation RunSessionProcess(
        SessionSpecification session,
        string manifestPath,
        string fixtureRoot)
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The host process path is unavailable.");
        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // A dotted assembly name defeats GetFileNameWithoutExtension, which would read
        // "PortableParity.Host" as "PortableParity"; compare against the apphost path instead.
        var entryAssembly = typeof(Program).Assembly.Location;
        var apphostPath = Path.ChangeExtension(
            entryAssembly,
            OperatingSystem.IsWindows() ? ".exe" : null);
        if (!string.Equals(executablePath, apphostPath, StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(entryAssembly);
        }

        startInfo.ArgumentList.Add("run-session");
        startInfo.ArgumentList.Add("--session");
        startInfo.ArgumentList.Add(session.Name);
        startInfo.ArgumentList.Add("--manifest");
        startInfo.ArgumentList.Add(manifestPath);
        startInfo.ArgumentList.Add("--fixtures");
        startInfo.ArgumentList.Add(fixtureRoot);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The session process failed to start.");
        // Both streams must drain concurrently. A session observation larger than the pipe
        // buffer, which is 4 KB on Windows against 64 KB elsewhere, blocks the child mid-write
        // while a sequential reader waits on the stream it is not draining.
        var outputReader = process.StandardOutput.ReadToEndAsync();
        var errorReader = process.StandardError.ReadToEndAsync();
        Task.WaitAll(outputReader, errorReader);
        process.WaitForExit();

        var standardOutput = outputReader.Result;
        var standardError = errorReader.Result;

        if (standardError.Length > 0)
        {
            Console.Error.Write(standardError);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Session '"
                + session.Name
                + "' exited with code "
                + process.ExitCode
                + ".");
        }

        return JsonSerializer.Deserialize<SessionObservation>(standardOutput, JsonOptions)
            ?? throw new InvalidDataException(
                "Session '" + session.Name + "' produced no observation.");
    }

    private static SessionObservation RunSession(
        SessionSpecification session,
        string fixtureRoot)
    {
        var basePath = AppContext.BaseDirectory;
        var applicationPath = Path.GetFullPath(
            Path.Combine(fixtureRoot, session.Fixture));
        var applicationId = "portable-parity:" + session.Name;

        InPhase("fixture-validation", () => ValidateFixture(applicationPath));
        InPhase(
            "host-registration",
            () => WebFormsApplication.Initialize(new WebFormsApplicationOptions
            {
                ApplicationId = applicationId,
                PhysicalRootPath = applicationPath,
                VirtualRootPath = "/",
                MachineConfigurationFilePath = Path.Combine(
                    basePath,
                    "configs",
                    "rehost-webforms.machine.config"),
                RootWebConfigurationFilePath = Path.Combine(
                    basePath,
                    "configs",
                    "rehost-webforms.web.config"),
                // The fixture also declares this path as <compilation tempDirectory>, which the
                // Framework oracle needs because it has no host option. Supplying both exercises
                // host precedence and the agreement branch of the conflict check.
                CompilationTempDirectory = Path.GetFullPath(
                    Path.Combine(fixtureRoot, "temp"))
            }));

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
                    applicationId,
                    typeof(PortableRunner),
                    "/",
                    EnsureTrailingDirectorySeparator(applicationPath),
                    true,
                    true));
            applicationActivated = true;

            if (registered is not IClassicPipelineRunner runner)
            {
                throw new InvalidOperationException(
                    "ApplicationManager did not return an IClassicPipelineRunner.");
            }

            var observation = new SessionObservation { Name = session.Name };

            foreach (var step in session.Steps)
            {
                observation.Requests.AddRange(
                    InPhase(
                        "step:" + string.Join(",", step.Select(request => request.Name)),
                        () => runner.RunStep(step)));
            }

            // StopObject runs the registered object's shutdown notification while the
            // application is still callable; ShutdownApplication is what tears it down.
            InPhase(
                "application-cleanup",
                () => manager.StopObject(applicationId, typeof(PortableRunner)));
            observation.ApplicationEvents = InPhase(
                "application-cleanup",
                runner.DrainApplicationEvents);
            observation.SessionEvents = InPhase(
                "application-cleanup",
                runner.DrainSessionEvents);
            applicationActivated = false;

            InPhase(
                "application-cleanup",
                () =>
                {
                    manager.ShutdownApplication(applicationId);
                    manager.Close();
                });

            return observation;
        }
        finally
        {
            if (applicationActivated)
            {
                InPhase(
                    "application-cleanup",
                    () =>
                    {
                        manager.StopObject(applicationId, typeof(PortableRunner));
                        manager.ShutdownApplication(applicationId);
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

        ObservationComparer.VerifySessions(expected.Sessions, actual.Sessions);
        Console.Error.WriteLine(
            "Portable observation strictly matches the Framework golden trace across "
            + actual.Sessions.Count
            + " session(s).");
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
                "The parity normalization manifest must remain empty.");
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
        catch (PhaseException)
        {
            throw;
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
        catch (PhaseException)
        {
            throw;
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
        internal static void VerifySessions(
            IReadOnlyList<SessionObservation> expected,
            IReadOnlyList<SessionObservation> actual)
        {
            VerifyValue("$.Sessions.Count", expected.Count, actual.Count);

            for (var index = 0; index < expected.Count; index++)
            {
                var path = "$.Sessions[" + expected[index].Name + "]";
                VerifyValue(path + ".Name", expected[index].Name, actual[index].Name);
                VerifyList(
                    path + ".ApplicationEvents",
                    expected[index].ApplicationEvents,
                    actual[index].ApplicationEvents,
                    VerifyValue);
                VerifyList(
                    path + ".SessionEvents",
                    expected[index].SessionEvents,
                    actual[index].SessionEvents,
                    VerifyValue);
                VerifyRequests(path, expected[index].Requests, actual[index].Requests);
            }
        }

        private static void VerifyRequests(
            string sessionPath,
            IReadOnlyList<RequestObservation> expected,
            IReadOnlyList<RequestObservation> actual)
        {
            VerifyValue(sessionPath + ".Requests.Count", expected.Count, actual.Count);

            for (var index = 0; index < expected.Count; index++)
            {
                var path = sessionPath + ".Requests[" + expected[index].Name + "]";
                VerifyValue(path + ".Name", expected[index].Name, actual[index].Name);
                Verify(path, expected[index].Observation, actual[index].Observation);
            }
        }

        private static void Verify(
            string path,
            PipelineObservation expected,
            PipelineObservation actual)
        {
            VerifyList(
                path + ".Events",
                expected.Events,
                actual.Events,
                VerifyValue);
            VerifyValue(
                path + ".Response.StatusCode",
                expected.Response.StatusCode,
                actual.Response.StatusCode);
            VerifyValue(
                path + ".Response.StatusDescription",
                expected.Response.StatusDescription,
                actual.Response.StatusDescription);
            VerifyList(
                path + ".Response.Headers",
                expected.Response.Headers,
                actual.Response.Headers,
                (headerPath, left, right) =>
                {
                    VerifyValue(headerPath + ".Name", left.Name, right.Name);
                    VerifyValue(headerPath + ".Value", left.Value, right.Value);
                });
            VerifyValue(
                path + ".Response.BodyBase64",
                expected.Response.BodyBase64,
                actual.Response.BodyBase64);
            VerifyList(
                path + ".Response.Flushes",
                expected.Response.Flushes,
                actual.Response.Flushes,
                VerifyValue);
            VerifyException(
                path + ".EscapedException",
                expected.EscapedException,
                actual.EscapedException);
            VerifyValue(
                path + ".EndOfRequestCount",
                expected.EndOfRequestCount,
                actual.EndOfRequestCount);
            VerifyValue(
                path + ".CompletionCount",
                expected.CompletionCount,
                actual.CompletionCount);
        }

        private static void VerifyList<T>(
            string path,
            IReadOnlyList<T> expected,
            IReadOnlyList<T> actual,
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
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw Mismatch(path, expected, actual);
            }
        }
    }
}

internal enum Operation
{
    Run,
    Verify,
    RunSession
}

internal sealed class CommandLine
{
    internal Operation Operation { get; private set; }

    internal string? ExpectedPath { get; private set; }

    internal string? NormalizationPath { get; private set; }

    internal string? ManifestPath { get; private set; }

    internal string? FixtureRoot { get; private set; }

    internal string? SessionName { get; private set; }

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
                "run-session" => Operation.RunSession,
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
                case "--manifest":
                    result.ManifestPath = value;
                    break;
                case "--fixtures":
                    result.FixtureRoot = value;
                    break;
                case "--session":
                    if (result.Operation != Operation.RunSession)
                    {
                        throw Usage("'--session' is valid only for run-session.");
                    }

                    result.SessionName = value;
                    break;
                default:
                    throw Usage("Unknown option '" + option + "'.");
            }
        }

        if (result.Operation == Operation.RunSession
            && string.IsNullOrWhiteSpace(result.SessionName))
        {
            throw Usage("run-session requires --session <name>.");
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
            + "  dotnet PortableParity.Host.dll run [--manifest <path>] [--fixtures <path>]"
            + Environment.NewLine
            + "  dotnet PortableParity.Host.dll verify [--expected <path>]"
            + " [--normalization <path>] [--manifest <path>] [--fixtures <path>]"
            + Environment.NewLine
            + "  dotnet PortableParity.Host.dll run-session --session <name>"
            + " [--manifest <path>] [--fixtures <path>]");
    }
}
