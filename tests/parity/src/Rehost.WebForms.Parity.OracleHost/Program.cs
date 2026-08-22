using System;
using System.IO;
using System.Threading.Tasks;
using Rehost.WebForms.Parity.Contracts;
using Rehost.WebForms.Parity.Harness;
using Rehost.WebForms.Parity.Runner;
using Microsoft.Win32;

namespace Rehost.WebForms.Parity.OracleHost;

internal static class Program
{
    private const int MinimumNet481Release = 533320;

    public static Task<int> Main(string[] args) => ParityHostProgram.RunAsync(
        args,
        new ParityHostDefinition
        {
            ExecutableName = "Rehost.WebForms.Parity.OracleHost.exe",
            SchemaVersion = 2,
            // Frozen: compared against the committed golden.
            Provenance = new TraceProvenance
            {
                Oracle = "Microsoft .NET Framework",
                TargetFramework = "net481",
                RuntimeRequirement = "4.8.1",
                ManagedEntryPoint =
                    "System.Web.HttpRuntime.ProcessRequest(HttpWorkerRequest)",
                ActivationEntryPoint =
                    "System.Web.Hosting.ApplicationManager.CreateObject",
                Fixture = "precompiled-handler-and-request-body-v2"
            },
            SupportedOperations = new[]
            {
                ParityOperation.Run,
                ParityOperation.Generate,
                ParityOperation.Verify,
                ParityOperation.RunSession
            },
            Comparison = TraceComparison.Strict,
            // The oracle produces the goldens, so nothing next to it is trustworthy as one.
            RequireExpectedPath = true,
            VerifiedMessage = (trace, expected) => "Verified " + expected,
            HostAssembly = typeof(Program).Assembly,
            ValidateRuntime = () => Console.Error.WriteLine(
                "Validated .NET Framework 4.8.1 release key " + ValidateRuntime() + "."),
            OnSessionStarting = session => Console.Error.WriteLine(
                "Starting oracle session '" + session.Name + "'."),
            RunSession = (session, fixtureRoot) => Task.FromResult(
                ClassicPipelineSession.Run(
                    session,
                    "framework-oracle:" + session.Name,
                    Path.GetFullPath(Path.Combine(fixtureRoot, session.Fixture)),
                    typeof(OracleSessionRunner),
                    typeof(Program).Assembly,
                    beforeActivation: null))
        });

    private static int ValidateRuntime()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            throw new PlatformNotSupportedException(
                "The semantic oracle runs only on Windows with .NET Framework 4.8.1.");
        }

        var value = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",
            "Release",
            null);

        if (!(value is int release) || release < MinimumNet481Release)
        {
            throw new PlatformNotSupportedException(
                "Installed .NET Framework release key must be at least "
                + MinimumNet481Release
                + ".");
        }

        return release;
    }
}
