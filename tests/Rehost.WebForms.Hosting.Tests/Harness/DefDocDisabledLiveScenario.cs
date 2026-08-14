namespace Rehost.WebForms.Hosting.Tests;

public sealed class DefDocDisabledLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.DefDocDisabled);

    internal ScenarioClient Client => _host.Client;
}
