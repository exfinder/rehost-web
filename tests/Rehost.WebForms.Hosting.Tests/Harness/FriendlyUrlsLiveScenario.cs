namespace Rehost.WebForms.Hosting.Tests;

public sealed class FriendlyUrlsLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.FriendlyUrls);

    internal Uri Address => _host.Address;

    internal ScenarioClient Client => _host.Client;

    internal HostWitness Witness => _host.Witness;
}
