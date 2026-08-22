using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Hosting;
using Rehost.WebForms.Parity.Contracts;
using Rehost.WebForms.Parity.Harness;
using Rehost.WebForms.Parity.Runner;
using Rehost.WebForms.Hosting;

namespace Rehost.WebForms.Parity.PortableHost;

internal static class Program
{
    public static Task<int> Main(string[] args) => ParityHostProgram.RunAsync(
        args,
        new ParityHostDefinition
        {
            ExecutableName = "dotnet Rehost.WebForms.Parity.PortableHost.dll",
            SchemaVersion = 2,
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
            SupportedOperations =
            [
                ParityOperation.Run,
                ParityOperation.Verify,
                ParityOperation.RunSession
            ],
            Comparison = TraceComparison.Strict,
            VerifiedMessage = (trace, expected) =>
                "Portable observation strictly matches the Framework golden trace across "
                + trace.Sessions.Count
                + " session(s).",
            HostAssembly = typeof(Program).Assembly,
            RunSession = (session, fixtureRoot) =>
                Task.FromResult(RunSession(session, fixtureRoot))
        });

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
