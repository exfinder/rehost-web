namespace Rehost.WebForms.Hosting.Tests;

public sealed class SessionCustomLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.SessionCustom);

    internal ScenarioClient Client => _host.Client;

    internal HostWitness Witness => _host.Witness;
}
