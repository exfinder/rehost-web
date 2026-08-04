using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using CoreParity.Contracts;
using FrameworkOracle.Runner;
using Microsoft.Win32;

namespace FrameworkOracle.Host;

internal static class Program
{
    private const int MinimumNet481Release = 533320;
    private const int SchemaVersion = 2;

    public static int Main(string[] args)
    {
        try
        {
            var command = CommandLine.Parse(args);
            var release = ValidateRuntime();
            Console.Error.WriteLine(
                "Validated .NET Framework 4.8.1 release key " + release + ".");

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var manifestPath = command.ManifestPath
                ?? Path.Combine(basePath, "metadata", "sessions.json");
            var fixtureRoot = command.FixtureRoot ?? Path.Combine(basePath, "fixture");
            var manifest = LoadManifest(manifestPath);

            if (command.Operation == Operation.RunSession)
            {
                var session = manifest.Sessions.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.Name,
                        command.SessionName,
                        StringComparison.Ordinal));

                if (session == null)
                {
                    throw new InvalidOperationException(
                        "Manifest declares no session named '" + command.SessionName + "'.");
                }

                Console.Out.Write(
                    Encoding.UTF8.GetString(
                        Serialize<SessionObservation>(RunSession(session, fixtureRoot))));
                return 0;
            }

            var trace = new PipelineTrace
            {
                SchemaVersion = SchemaVersion,
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
                    .Select(session => RunSessionProcess(session, manifestPath, fixtureRoot))
                    .ToList()
            };

            var bytes = Serialize<PipelineTrace>(trace);

            switch (command.Operation)
            {
                case Operation.Run:
                    Console.Out.Write(Encoding.UTF8.GetString(bytes));
                    return 0;

                case Operation.Generate:
                    WriteGenerated(command.ArtifactPath!, bytes);
                    return 0;

                case Operation.Verify:
                    Verify(command.ArtifactPath!, bytes);
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

    private static SessionManifest LoadManifest(string path)
    {
        path = Path.GetFullPath(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Session manifest is absent.", path);
        }

        SessionManifest manifest;
        var serializer = new DataContractJsonSerializer(typeof(SessionManifest));

        using (var stream = File.OpenRead(path))
        {
            manifest = (SessionManifest)serializer.ReadObject(stream);
        }

        if (manifest == null)
        {
            throw new InvalidDataException("Session manifest deserialized to null.");
        }

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

    // Each session observes a cold runtime, so it needs a process whose HttpRuntime singleton
    // and activated application have never been touched.
    private static SessionObservation RunSessionProcess(
        SessionSpecification session,
        string manifestPath,
        string fixtureRoot)
    {
        var executablePath = new Uri(
            typeof(Program).Assembly.GetName().CodeBase).LocalPath;
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            Arguments = string.Join(
                " ",
                "run-session",
                "--session",
                Quote(session.Name),
                "--manifest",
                Quote(manifestPath),
                "--fixtures",
                Quote(fixtureRoot))
        };

        Console.Error.WriteLine("Starting oracle session '" + session.Name + "'.");

        using (var process = Process.Start(startInfo))
        {
            // Both streams must drain concurrently. A session observation larger than the 4 KB
            // Windows pipe buffer blocks the child mid-write while a sequential reader waits on
            // the stream it is not draining.
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

            var serializer = new DataContractJsonSerializer(typeof(SessionObservation));

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(standardOutput)))
            {
                var observation = (SessionObservation)serializer.ReadObject(stream);

                if (observation == null)
                {
                    throw new InvalidDataException(
                        "Session '" + session.Name + "' produced no observation.");
                }

                return observation;
            }
        }
    }

    private static SessionObservation RunSession(
        SessionSpecification session,
        string fixtureRoot)
    {
        var applicationPath = Path.GetFullPath(
            Path.Combine(fixtureRoot, session.Fixture));
        var applicationId = "framework-oracle:" + session.Name;

        ValidateFixture(applicationPath);

        var manager = ApplicationManager.GetApplicationManager();
        var applicationActivated = false;
        manager.Open();

        try
        {
            Console.Error.WriteLine("Activating ASP.NET application AppDomain.");
            var registered = manager.CreateObject(
                applicationId,
                typeof(OracleRunner),
                "/",
                EnsureTrailingDirectorySeparator(applicationPath),
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
            manager.StopObject(applicationId, typeof(OracleRunner));
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
                    manager.StopObject(applicationId, typeof(OracleRunner));
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

    private static void ValidateFixture(string applicationPath)
    {
        applicationPath = Path.GetFullPath(applicationPath);
        var binPath = Path.Combine(applicationPath, "bin");
        var probePath = Path.Combine(binPath, "CoreParity.Probes.dll");
        var hostProbePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
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
    }

    private static byte[] Serialize<T>(T value)
    {
        var serializer = new DataContractJsonSerializer(typeof(T));

        using (var stream = new MemoryStream())
        {
            serializer.WriteObject(stream, value);
            stream.WriteByte((byte)'\n');
            return stream.ToArray();
        }
    }

    private static void WriteGenerated(string path, byte[] bytes)
    {
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, bytes);
        Console.Error.WriteLine("Generated " + path);
    }

    private static void Verify(string path, byte[] actual)
    {
        path = Path.GetFullPath(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Golden trace is absent. Generate it on Windows; do not hand-author it.",
                path);
        }

        var expected = File.ReadAllBytes(path);

        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                "Generated observation differs from " + path + ".");
        }

        Console.Error.WriteLine("Verified " + path);
    }

    private static string Quote(string value)
    {
        return "\"" + value + "\"";
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        if (path.EndsWith(
                Path.DirectorySeparatorChar.ToString(),
                StringComparison.Ordinal))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}

internal enum Operation
{
    Run,
    Generate,
    Verify,
    RunSession
}

internal sealed class CommandLine
{
    internal Operation Operation { get; private set; }

    internal string? ArtifactPath { get; private set; }

    internal string? ManifestPath { get; private set; }

    internal string? FixtureRoot { get; private set; }

    internal string? SessionName { get; private set; }

    internal static CommandLine Parse(string[] args)
    {
        var result = new CommandLine();
        var index = 0;

        if (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
        {
            switch (args[index])
            {
                case "run":
                    result.Operation = Operation.Run;
                    break;
                case "generate":
                    result.Operation = Operation.Generate;
                    break;
                case "verify":
                    result.Operation = Operation.Verify;
                    break;
                case "run-session":
                    result.Operation = Operation.RunSession;
                    break;
                default:
                    throw Usage("Unknown command '" + args[index] + "'.");
            }

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
                case "--output":
                    RequireOperation(result.Operation, Operation.Generate, option);
                    result.ArtifactPath = value;
                    break;
                case "--expected":
                    RequireOperation(result.Operation, Operation.Verify, option);
                    result.ArtifactPath = value;
                    break;
                case "--manifest":
                    result.ManifestPath = value;
                    break;
                case "--fixtures":
                    result.FixtureRoot = value;
                    break;
                case "--session":
                    RequireOperation(result.Operation, Operation.RunSession, option);
                    result.SessionName = value;
                    break;
                default:
                    throw Usage("Unknown option '" + option + "'.");
            }
        }

        if ((result.Operation == Operation.Generate || result.Operation == Operation.Verify)
            && string.IsNullOrWhiteSpace(result.ArtifactPath))
        {
            throw Usage(
                result.Operation == Operation.Generate
                    ? "generate requires --output <path>."
                    : "verify requires --expected <path>.");
        }

        if (result.Operation == Operation.RunSession
            && string.IsNullOrWhiteSpace(result.SessionName))
        {
            throw Usage("run-session requires --session <name>.");
        }

        return result;
    }

    private static void RequireOperation(
        Operation actual,
        Operation required,
        string option)
    {
        if (actual != required)
        {
            throw Usage("'" + option + "' is not valid for this command.");
        }
    }

    private static ArgumentException Usage(string message)
    {
        return new ArgumentException(
            message
            + Environment.NewLine
            + "Usage:"
            + Environment.NewLine
            + "  FrameworkOracle.Host.exe run [--manifest <path>] [--fixtures <path>]"
            + Environment.NewLine
            + "  FrameworkOracle.Host.exe generate --output <path>"
            + " [--manifest <path>] [--fixtures <path>]"
            + Environment.NewLine
            + "  FrameworkOracle.Host.exe verify --expected <path>"
            + " [--manifest <path>] [--fixtures <path>]"
            + Environment.NewLine
            + "  FrameworkOracle.Host.exe run-session --session <name>"
            + " [--manifest <path>] [--fixtures <path>]");
    }
}
