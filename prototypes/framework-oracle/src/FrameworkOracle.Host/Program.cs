using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Web.Hosting;
using FrameworkOracle.Contracts;
using FrameworkOracle.Runner;
using Microsoft.Win32;

namespace FrameworkOracle.Host;

internal static class Program
{
    private const int MinimumNet481Release = 533320;
    private const string ApplicationId = "framework-oracle:cold-sync";

    public static int Main(string[] args)
    {
        try
        {
            var command = CommandLine.Parse(args);
            var release = ValidateRuntime();
            Console.Error.WriteLine(
                "Validated .NET Framework 4.8.1 release key " + release + ".");

            var applicationPath = command.ApplicationPath
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture", "app");

            ValidateFixture(applicationPath);
            var trace = RunColdSynchronous(applicationPath);
            var bytes = Serialize(trace);

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

    private static void ValidateFixture(string applicationPath)
    {
        applicationPath = Path.GetFullPath(applicationPath);
        var binPath = Path.Combine(applicationPath, "bin");
        var probePath = Path.Combine(binPath, "FrameworkOracle.Probes.dll");
        var hostProbePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "FrameworkOracle.Probes.dll");

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
                    "FrameworkOracle.Probes",
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Host assembly must not reference FrameworkOracle.Probes.");
        }
    }

    private static OracleTrace RunColdSynchronous(string applicationPath)
    {
        var manager = ApplicationManager.GetApplicationManager();
        manager.Open();

        try
        {
            Console.Error.WriteLine("Activating ASP.NET application AppDomain.");
            var registered = manager.CreateObject(
                ApplicationId,
                typeof(OracleRunner),
                "/",
                EnsureTrailingDirectorySeparator(Path.GetFullPath(applicationPath)),
                true,
                true);
            Console.Error.WriteLine("Activated ASP.NET application AppDomain.");

            if (!(registered is IOracleRunner runner))
            {
                throw new InvalidOperationException(
                    "ApplicationManager did not return an IOracleRunner proxy.");
            }

            var request = RequestSpecification.ColdSynchronous();
            Console.Error.WriteLine("Entering HttpRuntime.ProcessRequest.");
            var observation = runner.Run(request);
            Console.Error.WriteLine("Completed HttpRuntime.ProcessRequest.");

            return new OracleTrace
            {
                SchemaVersion = 1,
                Provenance = new OracleProvenance
                {
                    Oracle = "Microsoft .NET Framework",
                    TargetFramework = "net481",
                    RuntimeRequirement = "4.8.1",
                    ManagedEntryPoint =
                        "System.Web.HttpRuntime.ProcessRequest(HttpWorkerRequest)",
                    ActivationEntryPoint =
                        "System.Web.Hosting.ApplicationManager.CreateObject",
                    Fixture = "bodyless-precompiled-handler-v1"
                },
                Scenario = request.Scenario,
                Observation = observation
            };
        }
        finally
        {
            try
            {
                Console.Error.WriteLine("Stopping registered oracle runner.");
                manager.StopObject(ApplicationId, typeof(OracleRunner));
                Console.Error.WriteLine("Requesting ASP.NET application shutdown.");
                manager.ShutdownApplication(ApplicationId);
            }
            finally
            {
                Console.Error.WriteLine("Closing ApplicationManager.");
                manager.Close();
                Console.Error.WriteLine("Closed ApplicationManager.");
            }
        }
    }

    private static byte[] Serialize(OracleTrace trace)
    {
        var serializer = new DataContractJsonSerializer(typeof(OracleTrace));

        using (var stream = new MemoryStream())
        {
            serializer.WriteObject(stream, trace);
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
    Verify
}

internal sealed class CommandLine
{
    internal Operation Operation { get; private set; }

    internal string? ArtifactPath { get; private set; }

    internal string? ApplicationPath { get; private set; }

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
                case "--app":
                    result.ApplicationPath = value;
                    break;
                default:
                    throw Usage("Unknown option '" + option + "'.");
            }
        }

        if (result.Operation != Operation.Run
            && string.IsNullOrWhiteSpace(result.ArtifactPath))
        {
            throw Usage(
                result.Operation == Operation.Generate
                    ? "generate requires --output <path>."
                    : "verify requires --expected <path>.");
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
            + "  FrameworkOracle.Host.exe run [--app <path>]"
            + Environment.NewLine
            + "  FrameworkOracle.Host.exe generate --output <path> [--app <path>]"
            + Environment.NewLine
            + "  FrameworkOracle.Host.exe verify --expected <path> [--app <path>]");
    }
}
