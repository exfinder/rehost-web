namespace Rehost.WebForms.Hosting.Tests;

public sealed class AsyncAppLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.AsyncApp);

    internal ScenarioClient Client => _host.Client;

    internal HostWitness Witness => _host.Witness;
}
