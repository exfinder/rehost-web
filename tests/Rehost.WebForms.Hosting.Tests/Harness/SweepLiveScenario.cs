namespace Rehost.WebForms.Hosting.Tests;

// Dedicated host over the page payload: the sweep probe presents every registered request as
// expired, so any other class's in-flight request on the same host would be spuriously timed
// out. Never route this through the shared registry.
public sealed class SweepLiveScenario : IDisposable
{
    private readonly LiveScenario _host =
        LiveScenario.StartIsolated(Fixtures.Page, IsolationReason.ProcessDamage);

    internal ScenarioClient Client => _host.Client;

    internal HostWitness Witness => _host.Witness;

    public void Dispose() => _host.Dispose();
}
