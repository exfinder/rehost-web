using System.Diagnostics;
using Rehost.Web.ScenarioProtocol;

namespace Rehost.Web.TestSupport;

public static class ScenarioHostInvocation
{
    public static string HostDirectory { get; } =
        TestOutputPaths.TestProjectOutput("Rehost.Web.ScenarioHost");

    public static string HostAssemblyPath { get; } =
        Path.Combine(HostDirectory, "Rehost.Web.ScenarioHost.dll");

    public static string FixturePath(string fixtureName) =>
        Path.Combine(HostDirectory, "fixtures", fixtureName);
}

// The host refuses an option its mode does not honor; the builders make that unspellable from
// tests, because an option method exists only on the mode that honors it.
public abstract class ScenarioHostInvocation<TInvocation>
    where TInvocation : ScenarioHostInvocation<TInvocation>
{
    private readonly List<string> _arguments = [];
    private readonly Dictionary<string, string> _environment = [];

    public TInvocation Application(string path) => Add(ScenarioHostGrammar.App, path);

    public TInvocation EnvironmentVariable(string name, string value)
    {
        _environment[name] = value;
        return (TInvocation)this;
    }

    public TInvocation ApplicationId(string id) => Add(ScenarioHostGrammar.Id, id);

    public TInvocation CompilationTemp(string path) => Add(ScenarioHostGrammar.Temp, path);

    public TInvocation Trace(string path) => Add(ScenarioHostGrammar.Trace, path);

    public TInvocation ResponseDirectory(string path) => Add(ScenarioHostGrammar.ResponseDir, path);

    public TInvocation Request(string url) => Add(ScenarioHostGrammar.Request, url);

    public ScenarioHostProcess Start()
    {
        if (!File.Exists(ScenarioHostInvocation.HostAssemblyPath))
        {
            throw new InvalidOperationException(
                "Build the scenario host first: dotnet build tests/Rehost.Web.ScenarioHost");
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

        foreach (var pair in _environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
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
    public ServeInvocation() => Add(ScenarioHostGrammar.Serve);

    public ServeInvocation KestrelMaxBody(long bytes) =>
        Add(ScenarioHostGrammar.KestrelMaxBody, bytes.ToString());

    public ServeInvocation Http2() => Add(ScenarioHostGrammar.Http2);

    public ServeInvocation Postback(string probe) => Add(ScenarioHostGrammar.Postback, probe);
}

public sealed class BatchInvocation : ScenarioHostInvocation<BatchInvocation>
{
    public BatchInvocation MachineConfig(string path) => Add(ScenarioHostGrammar.MachineConfig, path);

    public BatchInvocation HoldGate(string mutexName) => Add(ScenarioHostGrammar.HoldGate, mutexName);
}
