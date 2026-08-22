namespace Rehost.WebForms.Hosting.Tests;

public sealed class AuthLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.Auth);

    internal ScenarioClient Client => _host.Client;

    internal HostWitness Witness => _host.Witness;
}
