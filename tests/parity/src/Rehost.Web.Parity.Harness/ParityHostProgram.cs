using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Rehost.Web.Parity.Contracts;

namespace Rehost.Web.Parity.Harness;

public sealed class ParityHostDefinition
{
    public string ExecutableName { get; set; } = "";

    public int SchemaVersion { get; set; }

    public TraceProvenance Provenance { get; set; } = new TraceProvenance();

    public ParityOperation[] SupportedOperations { get; set; } =
        Array.Empty<ParityOperation>();

    public TraceComparison Comparison { get; set; }

    public bool RequireExpectedPath { get; set; }

    public Func<PipelineTrace, string, string> VerifiedMessage { get; set; } =
        (trace, expected) => "Verified " + expected;

    public Assembly HostAssembly { get; set; } = null!;

    public Action? ValidateRuntime { get; set; }

    public Action<SessionSpecification>? OnSessionStarting { get; set; }

    public Func<SessionSpecification, string, Task<SessionObservation>> RunSession { get; set; }
        = null!;
}

// One orchestration for every parity host: parse, manifest, per-session child processes, the
// operation dispatch, and structured failure diagnostics. What varies per host arrives through
// the definition; the execution behind RunSession is the host's own.
public static class ParityHostProgram
{
    public static async Task<int> RunAsync(string[] args, ParityHostDefinition host)
    {
        using (new RuntimeDiagnosticListener())
        {
            try
            {
                var command = ParityCommandLine.Parse(
                    args,
                    host.SupportedOperations,
                    host.ExecutableName);
                host.ValidateRuntime?.Invoke();

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
                    var observation = await host.RunSession(session, fixtureRoot)
                        .ConfigureAwait(false);
                    Console.Out.WriteLine(ParityJson.Serialize(observation));
                    return 0;
                }

                if (command.Operation == ParityOperation.Verify
                    && host.RequireExpectedPath
                    && string.IsNullOrWhiteSpace(command.ExpectedPath))
                {
                    throw new ArgumentException("verify requires --expected <path>.");
                }

                var trace = new PipelineTrace
                {
                    SchemaVersion = host.SchemaVersion,
                    Provenance = host.Provenance,
                    Sessions = manifest.Sessions
                        .Select(session => PhaseRunner.InPhase(
                            "session:" + session.Name,
                            () =>
                            {
                                host.OnSessionStarting?.Invoke(session);
                                return SessionChildProcess.Run(
                                    host.HostAssembly,
                                    session,
                                    manifestPath,
                                    fixtureRoot);
                            }))
                        .ToList()
                };

                switch (command.Operation)
                {
                    case ParityOperation.Generate:
                        WriteGenerated(command.OutputPath!, trace);
                        return 0;

                    case ParityOperation.Verify:
                        PhaseRunner.InPhase(
                            "verification",
                            () =>
                            {
                                var expected = command.ExpectedPath
                                    ?? Path.Combine(basePath, "oracle", "sessions.json");
                                GoldenTrace.Verify(expected, trace, host.Comparison);
                                Console.Error.WriteLine(
                                    host.VerifiedMessage(trace, Path.GetFullPath(expected)));
                            });
                        return 0;

                    default:
                        Console.Out.WriteLine(ParityJson.Serialize(trace));
                        return 0;
                }
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
    }

    private static void WriteGenerated(string path, PipelineTrace trace)
    {
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ParityJson.SerializeIndented(trace) + "\n");
        Console.Error.WriteLine("Generated " + path);
    }
}
