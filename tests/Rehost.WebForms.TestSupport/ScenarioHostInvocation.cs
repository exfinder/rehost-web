using System.Diagnostics;
using Rehost.WebForms.Parity.Harness;

namespace Rehost.WebForms.TestSupport;

// Every ScenarioHost CLI option has its typed method here and nowhere else.
public sealed class ScenarioHostInvocation
{
    private readonly List<string> _arguments = [];

    public static string HostDirectory { get; } =
        TestOutputPaths.TestProjectOutput("Rehost.WebForms.ScenarioHost");

    public static string HostAssemblyPath { get; } =
        Path.Combine(HostDirectory, "Rehost.WebForms.ScenarioHost.dll");

    public static string FixturePath(string fixtureName) =>
        Path.Combine(HostDirectory, "fixtures", fixtureName);

    public ScenarioHostInvocation Serve() => Add("--serve");

    public ScenarioHostInvocation Application(string path) => Add("--app", path);

    public ScenarioHostInvocation ApplicationId(string id) => Add("--id", id);

    public ScenarioHostInvocation CompilationTemp(string path) => Add("--temp", path);

    public ScenarioHostInvocation Trace(string path) => Add("--trace", path);

    public ScenarioHostInvocation ResponseDirectory(string path) => Add("--response-dir", path);

    public ScenarioHostInvocation MachineConfig(string path) => Add("--machine-config", path);

    public ScenarioHostInvocation HoldGate(string mutexName) => Add("--hold-gate", mutexName);

    public ScenarioHostInvocation KestrelMaxBody(long bytes) =>
        Add("--kestrel-max-body", bytes.ToString());

    public ScenarioHostInvocation Http2() => Add("--http2");

    public ScenarioHostInvocation Request(string url) => Add("--request", url);

    public ScenarioHostInvocation Postback(string probe) => Add("--postback", probe);

    public ScenarioHostProcess Start()
    {
        if (!File.Exists(HostAssemblyPath))
        {
            throw new InvalidOperationException(
                "Build the scenario host first: dotnet build tests/Rehost.WebForms.ScenarioHost");
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = HostDirectory,
        };
        startInfo.ArgumentList.Add(HostAssemblyPath);
        foreach (var argument in _arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new ScenarioHostProcess(Process.Start(startInfo)!);
    }

    private ScenarioHostInvocation Add(params string[] arguments)
    {
        _arguments.AddRange(arguments);
        return this;
    }
}
