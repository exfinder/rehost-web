using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Hosting;
using Rehost.Web.Parity.Contracts;
using Rehost.Web.Parity.Harness;
using Rehost.Web.Parity.Runner;
using Rehost.Web.Hosting;

namespace Rehost.Web.Parity.PortableHost;

internal static class Program
{
    public static Task<int> Main(string[] args) => ParityHostProgram.RunAsync(
        args,
        new ParityHostDefinition
        {
            ExecutableName = "dotnet Rehost.Web.Parity.PortableHost.dll",
            SchemaVersion = 2,
            // Frozen: compared against the committed golden.
            Provenance = new TraceProvenance
            {
                Oracle = "Rehost.Web portable runtime",
                TargetFramework = "net10.0",
                RuntimeRequirement = ".NET 10",
                ManagedEntryPoint =
                    "System.Web.HttpRuntime.ProcessRequest(HttpWorkerRequest)",
                ActivationEntryPoint =
                    "RehostWebApplication.Initialize + ApplicationManager.CreateObject",
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

        return ClassicPipelineSession.Run(
            session,
            applicationId,
            applicationPath,
            typeof(PortableSessionRunner),
            typeof(Program).Assembly,
            beforeActivation: () => RehostWebApplication.Initialize(new RehostWebOptions
            {
                ApplicationId = applicationId,
                PhysicalRootPath = applicationPath,
                VirtualRootPath = "/",
                MachineConfigurationFilePath = Path.Combine(
                    basePath,
                    "configs",
                    RehostWebOptions.DefaultMachineConfigurationFileName),
                RootWebConfigurationFilePath = Path.Combine(
                    basePath,
                    "configs",
                    RehostWebOptions.DefaultRootWebConfigurationFileName),
                // The fixture also declares this path as <compilation tempDirectory>, which the
                // Framework oracle needs because it has no host option. Supplying both exercises
                // host precedence and the agreement branch of the conflict check.
                CompilationTempDirectory = Path.GetFullPath(
                    Path.Combine(fixtureRoot, "temp")),
                MachineKeyDirectory = Path.GetFullPath(
                    Path.Combine(fixtureRoot, "temp", "machine-keys"))
            }));
    }
}
