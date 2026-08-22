using System.Diagnostics;

namespace Rehost.WebForms.TestSupport;

public static class ScenarioHostInvocation
{
    public static string HostDirectory { get; } =
        TestOutputPaths.TestProjectOutput("Rehost.WebForms.ScenarioHost");

    public static string HostAssemblyPath { get; } =
        Path.Combine(HostDirectory, "Rehost.WebForms.ScenarioHost.dll");

    public static string FixturePath(string fixtureName) =>
        Path.Combine(HostDirectory, "fixtures", fixtureName);
}

// The host refuses an option its mode does not honor; the builders make that unspellable from
// tests, because an option method exists only on the mode that honors it.
public abstract class ScenarioHostInvocation<TInvocation>
    where TInvocation : ScenarioHostInvocation<TInvocation>
{
    private readonly List<string> _arguments = [];

    public TInvocation Application(string path) => Add("--app", path);

    public TInvocation ApplicationId(string id) => Add("--id", id);

    public TInvocation CompilationTemp(string path) => Add("--temp", path);

    public TInvocation Trace(string path) => Add("--trace", path);

    public TInvocation ResponseDirectory(string path) => Add("--response-dir", path);

    public TInvocation Request(string url) => Add("--request", url);

    public ScenarioHostProcess Start()
    {
        if (!File.Exists(ScenarioHostInvocation.HostAssemblyPath))
        {
            throw new InvalidOperationException(
                "Build the scenario host first: dotnet build tests/Rehost.WebForms.ScenarioHost");
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = ScenarioHostInvocation.HostDirectory,
        };
        startInfo.ArgumentList.Add(ScenarioHostInvocation.HostAssemblyPath);
        foreach (var argument in _arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new ScenarioHostProcess(Process.Start(startInfo)!);
    }

    protected TInvocation Add(params string[] arguments)
    {
        _arguments.AddRange(arguments);
        return (TInvocation)this;
    }
}

public sealed class ServeInvocation : ScenarioHostInvocation<ServeInvocation>
{
    public ServeInvocation() => Add("--serve");

    public ServeInvocation KestrelMaxBody(long bytes) =>
        Add("--kestrel-max-body", bytes.ToString());

    public ServeInvocation Http2() => Add("--http2");

    public ServeInvocation Postback(string probe) => Add("--postback", probe);
}

public sealed class BatchInvocation : ScenarioHostInvocation<BatchInvocation>
{
    public BatchInvocation MachineConfig(string path) => Add("--machine-config", path);

    public BatchInvocation HoldGate(string mutexName) => Add("--hold-gate", mutexName);
}
