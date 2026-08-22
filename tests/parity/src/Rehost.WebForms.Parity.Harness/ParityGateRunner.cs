#if NET
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Parity.Harness;

// Orchestrates a parity column from a test process: one child per session, comparison in-proc.
// The process tree never goes deeper than test -> session child.
public sealed class ParityGateRunner
{
    private readonly Lazy<Contracts.SessionManifest> manifest;
    private readonly Lazy<Contracts.PipelineTrace> golden;

    public ParityGateRunner(string hostProjectName)
    {
        RepositoryRoot = RepositoryLocator.FindRoot(AppContext.BaseDirectory);

        HostDirectory = Path.Combine(
            RepositoryRoot, "tests", "parity", "src", hostProjectName,
            "bin", TestOutputPaths.Configuration(), "net10.0");
        HostAssemblyPath = Path.Combine(HostDirectory, hostProjectName + ".dll");
        ManifestPath = Path.Combine(RepositoryRoot, "tests", "parity", "sessions.json");
        GoldenPath = Path.Combine(
            RepositoryRoot, "tests", "parity", "artifacts", "golden", "sessions.json");
        DefaultFixtureRoot = Path.Combine(HostDirectory, "fixture");

        manifest = new(() => ManifestLoader.Load(ManifestPath));
        golden = new(() => GoldenTrace.Load(GoldenPath));
    }

    public string RepositoryRoot { get; }

    public string HostDirectory { get; }

    public string HostAssemblyPath { get; }

    public string ManifestPath { get; }

    public string GoldenPath { get; }

    public string DefaultFixtureRoot { get; }

    public IReadOnlyList<string> SessionNames =>
        manifest.Value.Sessions.Select(session => session.Name).ToList();

    public IReadOnlyList<Contracts.SessionSpecification> Sessions => manifest.Value.Sessions;

    public int GoldenSchemaVersion => golden.Value.SchemaVersion;

    public Contracts.SessionObservation RunSession(string name, string? fixtureRoot = null)
    {
        EnsureHostBuilt();
        var session = ManifestLoader.FindSession(manifest.Value, name);

        return SessionChildProcess.Run(
            HostAssemblyPath,
            session,
            ManifestPath,
            fixtureRoot ?? DefaultFixtureRoot);
    }

    public void VerifySession(string name, TraceComparison comparison)
    {
        var expected = golden.Value.Sessions.SingleOrDefault(
                session => string.Equals(session.Name, name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "Golden trace records no session named '" + name + "'.");

        var actual = RunSession(name);

        TraceComparer.VerifySessions(new[] { expected }, new[] { actual }, comparison);
    }

    private void EnsureHostBuilt()
    {
        if (!File.Exists(HostAssemblyPath))
        {
            throw new FileNotFoundException(
                "Build the parity host first: dotnet build Rehost.WebForms.slnx",
                HostAssemblyPath);
        }
    }
}
#endif
