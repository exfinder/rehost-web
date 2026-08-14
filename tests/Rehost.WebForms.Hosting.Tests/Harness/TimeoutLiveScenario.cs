namespace Rehost.WebForms.Hosting.Tests;

// The timeout probes hang and expire their requests, so the host is damaged goods: one private
// process per consuming class, never the registry.
public sealed class TimeoutLiveScenario : IDisposable
{
    private readonly LiveScenario _host =
        LiveScenario.StartIsolated(Fixtures.Timeout, IsolationReason.ProcessDamage);

    internal ScenarioClient Client => _host.Client;

    internal HostWitness Witness => _host.Witness;

    public void Dispose() => _host.Dispose();
}
