using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Rehost.Web.ScenarioProtocol;
using Rehost.Web.TestSupport;

namespace Rehost.Web.AspNetCore.Tests;

// One passively-serving host process for a fixture; tests act through the client and assert on
// the response, reaching for the witness only for server-side facts.
//
// Two doors, no third: shared hosts come from ScenarioHostRegistry.GetOrAdd, and a private
// process exists only through StartIsolated, which demands a structural IsolationReason. The
// constructor stays private so a same-configuration duplicate host cannot be expressed.
public sealed class LiveScenario : IDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);

    private readonly StagedApplication _staged;
    private readonly long? _kestrelMaxBody;
    private readonly bool _http2;
    private readonly IReadOnlyDictionary<string, string>? _environment;
    private ScenarioHostProcess _process;

    // ScenarioHostRegistry's door only; every other caller goes through StartIsolated.
    internal static LiveScenario StartPooled(ScenarioFixture fixture) => new(fixture);

    internal static LiveScenario StartIsolated(
        ScenarioFixture fixture,
        IsolationReason reason,
        string? rootPath = null,
        long? kestrelMaxBody = null,
        bool http2 = false,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        _ = reason;
        return new(fixture, rootPath, kestrelMaxBody, http2, environment);
    }

    private LiveScenario(
        ScenarioFixture fixture,
        string? rootPath = null,
        long? kestrelMaxBody = null,
        bool http2 = false,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        _staged = StagedApplication.Stage(fixture.Name, rootPath);
        _kestrelMaxBody = kestrelMaxBody;
        _http2 = http2;
        _environment = environment;

        StartHost();
    }

    // The staged copy survives; the address and client do not — re-read them after the call.
    internal void KillAndRestart()
    {
        Client.Dispose();
        _process.Dispose();
        _staged.ResetTrace();

        StartHost();
    }

    [MemberNotNull(nameof(_process), nameof(Address), nameof(Client))]
    private void StartHost()
    {
        var invocation = _staged.Serve();
        if (_kestrelMaxBody != null)
        {
            invocation.KestrelMaxBody(_kestrelMaxBody.Value);
        }

        if (_http2)
        {
            invocation.Http2();
        }

        foreach (var pair in _environment ?? new Dictionary<string, string>())
        {
            invocation.EnvironmentVariable(pair.Key, pair.Value);
        }

        _process = invocation.Start();

        Address = new Uri(WaitForAddress());
        Client = new ScenarioClient(Address, http2: _http2);
    }

    internal string ApplicationPath => _staged.ApplicationPath;

    internal string MachineKeyDirectory => _staged.MachineKeyDirectory;

    internal int HostProcessId => _process.Id;

    internal bool WaitForExit(TimeSpan timeout) => _process.WaitForExit(timeout);

    internal int ExitCode => _process.ExitCode;

    internal Uri Address { get; private set; }

    internal ScenarioClient Client { get; private set; }

    internal HostWitness Witness => new(Client);

    private string WaitForAddress()
    {
        var deadline = Stopwatch.StartNew();

        while (deadline.Elapsed < StartupTimeout)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    "The scenario host exited before publishing an address. "
                    + _process.StandardError);
            }

            var address = ReadAddress();
            if (address != null)
            {
                return address;
            }

            Thread.Sleep(20);
        }

        Dispose();
        throw new TimeoutException(
            "The scenario host did not publish an address within "
            + StartupTimeout.TotalSeconds
            + "s. "
            + _process.StandardError);
    }

    private string? ReadAddress() => _staged.Trace()
        .Where(line => line.StartsWith(TraceEvents.Address, StringComparison.Ordinal))
        .Select(line => line[TraceEvents.Address.Length..])
        .FirstOrDefault();

    public void Dispose()
    {
        Client?.Dispose();
        _process.Dispose();
        _staged.Dispose();
    }
}
