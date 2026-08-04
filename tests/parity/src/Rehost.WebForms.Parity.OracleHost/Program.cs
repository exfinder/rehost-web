using System;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using Rehost.WebForms.Parity.Contracts;
using Rehost.WebForms.Parity.Harness;
using Rehost.WebForms.Parity.Runner;
using Microsoft.Win32;

namespace Rehost.WebForms.Parity.OracleHost;

internal static class Program
{
    private const int MinimumNet481Release = 533320;
    private const int SchemaVersion = 2;

    private static readonly ParityOperation[] SupportedOperations =
    {
        ParityOperation.Run,
        ParityOperation.Generate,
        ParityOperation.Verify,
        ParityOperation.RunSession
    };

    public static int Main(string[] args)
    {
        try
        {
            var command = ParityCommandLine.Parse(
                args,
                SupportedOperations,
                "Rehost.WebForms.Parity.OracleHost.exe");
            var release = ValidateRuntime();
            Console.Error.WriteLine(
                "Validated .NET Framework 4.8.1 release key " + release + ".");

            var basePath = AppContext.BaseDirectory;
            var manifestPath = command.ManifestPath
                ?? Path.Combine(basePath, "metadata", "sessions.json");
            var fixtureRoot = command.FixtureRoot ?? Path.Combine(basePath, "fixture");
            var manifest = ManifestLoader.Load(manifestPath);

            if (command.Operation == ParityOperation.RunSession)
            {
                var session = ManifestLoader.FindSession(manifest, command.SessionName!);
                Console.Out.WriteLine(ParityJson.Serialize(RunSession(session, fixtureRoot)));
                return 0;
            }

            if (command.Operation == ParityOperation.Verify
                && string.IsNullOrWhiteSpace(command.ExpectedPath))
            {
                throw new ArgumentException("verify requires --expected <path>.");
            }

            var trace = new PipelineTrace
            {
                SchemaVersion = SchemaVersion,
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
                Sessions = manifest.Sessions
                    .Select(session =>
                    {
                        Console.Error.WriteLine(
                            "Starting oracle session '" + session.Name + "'.");
                        return SessionChildProcess.Run(
                            typeof(Program).Assembly,
                            session,
                            manifestPath,
                            fixtureRoot);
                    })
                    .ToList()
            };

            switch (command.Operation)
            {
                case ParityOperation.Run:
                    Console.Out.WriteLine(ParityJson.Serialize(trace));
                    return 0;

                case ParityOperation.Generate:
                    WriteGenerated(command.OutputPath!, trace);
                    return 0;

                case ParityOperation.Verify:
                    GoldenTrace.Verify(
                        command.ExpectedPath!,
                        trace,
                        TraceComparison.Strict);
                    Console.Error.WriteLine("Verified " + Path.GetFullPath(command.ExpectedPath!));
                    return 0;

                default:
                    throw new InvalidOperationException("Unknown operation.");
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

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

    private static SessionObservation RunSession(
        SessionSpecification session,
        string fixtureRoot)
    {
        var applicationPath = Path.GetFullPath(
            Path.Combine(fixtureRoot, session.Fixture));
        var applicationId = "framework-oracle:" + session.Name;

        FixtureValidator.Validate(applicationPath, typeof(Program).Assembly);

        var manager = ApplicationManager.GetApplicationManager();
        var applicationActivated = false;
        manager.Open();

        try
        {
            Console.Error.WriteLine("Activating ASP.NET application AppDomain.");
            var registered = manager.CreateObject(
                applicationId,
                typeof(OracleSessionRunner),
                "/",
                PathUtilities.EnsureTrailingDirectorySeparator(applicationPath),
                true,
                true);
            applicationActivated = true;
            Console.Error.WriteLine("Activated ASP.NET application AppDomain.");

            var runner = registered as IClassicPipelineRunner;

            if (runner == null)
            {
                throw new InvalidOperationException(
                    "ApplicationManager did not return an IClassicPipelineRunner proxy.");
            }

            var observation = new SessionObservation { Name = session.Name };

            foreach (var step in session.Steps)
            {
                var names = string.Join(
                    ",",
                    step.Select(request => request.Name).ToArray());
                Console.Error.WriteLine("Entering step '" + names + "'.");
                observation.Requests.AddRange(runner.RunStep(step));
                Console.Error.WriteLine("Completed step '" + names + "'.");
            }

            // StopObject runs the registered object's shutdown notification while the
            // application AppDomain is still callable; ShutdownApplication unloads it.
            Console.Error.WriteLine("Stopping registered oracle runner.");
            manager.StopObject(applicationId, typeof(OracleSessionRunner));
            observation.ApplicationEvents = runner.DrainApplicationEvents();
            observation.SessionEvents = runner.DrainSessionEvents();
            applicationActivated = false;

            Console.Error.WriteLine("Requesting ASP.NET application shutdown.");
            manager.ShutdownApplication(applicationId);
            Console.Error.WriteLine("Closing ApplicationManager.");
            manager.Close();
            Console.Error.WriteLine("Closed ApplicationManager.");

            return observation;
        }
        finally
        {
            if (applicationActivated)
            {
                try
                {
                    Console.Error.WriteLine("Stopping registered oracle runner.");
                    manager.StopObject(applicationId, typeof(OracleSessionRunner));
                    Console.Error.WriteLine("Requesting ASP.NET application shutdown.");
                    manager.ShutdownApplication(applicationId);
                }
                finally
                {
                    Console.Error.WriteLine("Closing ApplicationManager.");
                    manager.Close();
                    Console.Error.WriteLine("Closed ApplicationManager.");
                }
            }
        }
    }

    private static void WriteGenerated(string path, PipelineTrace trace)
    {
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ParityJson.Serialize(trace) + "\n");
        Console.Error.WriteLine("Generated " + path);
    }
}
