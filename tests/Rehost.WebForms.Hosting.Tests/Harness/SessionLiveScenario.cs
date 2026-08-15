namespace Rehost.WebForms.Hosting.Tests;

public sealed class SessionLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.Session);

    internal ScenarioClient Client => _host.Client;

    internal HostWitness Witness => _host.Witness;
}
