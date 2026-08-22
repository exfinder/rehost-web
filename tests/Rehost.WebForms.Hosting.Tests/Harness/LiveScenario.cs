using System.Diagnostics;
using Rehost.WebForms.Parity.Contracts;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Hosting.Tests;

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
    private readonly ScenarioHostProcess _process;

    // ScenarioHostRegistry's door only; every other caller goes through StartIsolated.
    internal static LiveScenario StartPooled(ScenarioFixture fixture) => new(fixture);

    internal static LiveScenario StartIsolated(
        ScenarioFixture fixture,
        IsolationReason reason,
        string? rootPath = null,
        long? kestrelMaxBody = null,
        bool http2 = false)
    {
        _ = reason;
        return new(fixture, rootPath, kestrelMaxBody, http2);
    }

    private LiveScenario(
        ScenarioFixture fixture,
        string? rootPath = null,
        long? kestrelMaxBody = null,
        bool http2 = false)
    {
        _staged = StagedApplication.Stage(fixture.Name, rootPath);

        var invocation = _staged.Serve();
        if (kestrelMaxBody != null)
        {
            invocation.KestrelMaxBody(kestrelMaxBody.Value);
        }

        if (http2)
        {
            invocation.Http2();
        }

        _process = invocation.Start();

        Address = new Uri(WaitForAddress());
        Client = new ScenarioClient(Address, http2: http2);
    }

    internal string ApplicationPath => _staged.ApplicationPath;

    internal int HostProcessId => _process.Id;

    internal Uri Address { get; }

    internal ScenarioClient Client { get; }

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
