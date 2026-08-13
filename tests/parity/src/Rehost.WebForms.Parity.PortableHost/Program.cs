using System;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using Rehost.WebForms.Parity.Contracts;
using Rehost.WebForms.Parity.Harness;
using Rehost.WebForms.Parity.Runner;
using Rehost.WebForms.Hosting;

namespace Rehost.WebForms.Parity.PortableHost;

internal static class Program
{
    private const int SchemaVersion = 2;

    private static readonly ParityOperation[] SupportedOperations =
    {
        ParityOperation.Run,
        ParityOperation.Verify,
        ParityOperation.RunSession
    };

    public static int Main(string[] args)
    {
        using var diagnostics = new RuntimeDiagnosticListener();

        try
        {
            var command = ParityCommandLine.Parse(
                args,
                SupportedOperations,
                "dotnet Rehost.WebForms.Parity.PortableHost.dll");
            var basePath = AppContext.BaseDirectory;
            var manifestPath = command.ManifestPath
                ?? Path.Combine(basePath, "metadata", "sessions.json");
            var fixtureRoot = command.FixtureRoot ?? Path.Combine(basePath, "fixture");
            var manifest = PhaseRunner.InPhase(
                "manifest",
                () => ManifestLoader.Load(manifestPath));

            if (command.Operation == ParityOperation.RunSession)
            {
                var session = ManifestLoader.FindSession(manifest, command.SessionName!);
                Console.Out.WriteLine(ParityJson.Serialize(RunSession(session, fixtureRoot)));
                return 0;
            }

            var trace = new PipelineTrace
            {
                SchemaVersion = SchemaVersion,
                // Frozen: compared against the committed golden.
                Provenance = new TraceProvenance
                {
                    Oracle = "Rehost WebForms portable runtime",
                    TargetFramework = "net10.0",
                    RuntimeRequirement = ".NET 10",
                    ManagedEntryPoint =
                        "System.Web.HttpRuntime.ProcessRequest(HttpWorkerRequest)",
                    ActivationEntryPoint =
                        "WebFormsApplication.Initialize + ApplicationManager.CreateObject",
                    Fixture = "precompiled-handler-and-request-body-v2"
                },
                Sessions = manifest.Sessions
                    .Select(session => PhaseRunner.InPhase(
                        "session:" + session.Name,
                        () => SessionChildProcess.Run(
                            typeof(Program).Assembly,
                            session,
                            manifestPath,
                            fixtureRoot)))
                    .ToList()
            };

            if (command.Operation == ParityOperation.Verify)
            {
                PhaseRunner.InPhase(
                    "verification",
                    () =>
                    {
                        GoldenTrace.Verify(
                            command.ExpectedPath
                                ?? Path.Combine(basePath, "oracle", "sessions.json"),
                            trace,
                            TraceComparison.Strict);
                        Console.Error.WriteLine(
                            "Portable observation strictly matches the Framework golden trace across "
                            + trace.Sessions.Count
                            + " session(s).");
                    });
            }
            else
            {
                Console.Out.WriteLine(ParityJson.Serialize(trace));
            }

            return 0;
        }
        catch (PhaseException exception)
        {
            DiagnosticWriter.Write(exception.Phase, exception.InnerException!);
            return 1;
        }
        catch (Exception exception)
        {
            DiagnosticWriter.Write("command-line", exception);
            return 1;
        }
    }

    private static SessionObservation RunSession(
        SessionSpecification session,
        string fixtureRoot)
    {
        var basePath = AppContext.BaseDirectory;
        var applicationPath = Path.GetFullPath(
            Path.Combine(fixtureRoot, session.Fixture));
        var applicationId = "portable-parity:" + session.Name;

        PhaseRunner.InPhase(
            "fixture-validation",
            () => FixtureValidator.Validate(applicationPath, typeof(Program).Assembly));
        PhaseRunner.InPhase(
            "host-registration",
            () => WebFormsApplication.Initialize(new WebFormsApplicationOptions
            {
                ApplicationId = applicationId,
                PhysicalRootPath = applicationPath,
                VirtualRootPath = "/",
                MachineConfigurationFilePath = Path.Combine(
                    basePath,
                    "configs",
                    WebFormsApplicationOptions.DefaultMachineConfigurationFileName),
                RootWebConfigurationFilePath = Path.Combine(
                    basePath,
                    "configs",
                    WebFormsApplicationOptions.DefaultRootWebConfigurationFileName),
                // The fixture also declares this path as <compilation tempDirectory>, which the
                // Framework oracle needs because it has no host option. Supplying both exercises
                // host precedence and the agreement branch of the conflict check.
                CompilationTempDirectory = Path.GetFullPath(
                    Path.Combine(fixtureRoot, "temp"))
            }));

        var manager = PhaseRunner.InPhase(
            "application-activation",
            ApplicationManager.GetApplicationManager);
        var applicationActivated = false;
        PhaseRunner.InPhase("application-activation", manager.Open);

        try
        {
            var registered = PhaseRunner.InPhase(
                "application-activation",
                () => manager.CreateObject(
                    applicationId,
                    typeof(PortableSessionRunner),
                    "/",
                    PathUtilities.EnsureTrailingDirectorySeparator(applicationPath),
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
                    PhaseRunner.InPhase(
                        "step:" + string.Join(",", step.Select(request => request.Name)),
                        () => runner.RunStep(step)));
            }

            // StopObject runs the registered object's shutdown notification while the
            // application is still callable; ShutdownApplication is what tears it down.
            PhaseRunner.InPhase(
                "application-cleanup",
                () => manager.StopObject(applicationId, typeof(PortableSessionRunner)));
            observation.ApplicationEvents = PhaseRunner.InPhase(
                "application-cleanup",
                runner.DrainApplicationEvents);
            observation.SessionEvents = PhaseRunner.InPhase(
                "application-cleanup",
                runner.DrainSessionEvents);
            applicationActivated = false;

            PhaseRunner.InPhase(
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
                PhaseRunner.InPhase(
                    "application-cleanup",
                    () =>
                    {
                        manager.StopObject(applicationId, typeof(PortableSessionRunner));
                        manager.ShutdownApplication(applicationId);
                        manager.Close();
                    });
            }
        }
    }
}
