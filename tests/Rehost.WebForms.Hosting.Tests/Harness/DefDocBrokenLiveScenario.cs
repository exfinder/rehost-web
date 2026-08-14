namespace Rehost.WebForms.Hosting.Tests;

public sealed class DefDocBrokenLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.DefDocBroken);

    internal ScenarioClient Client => _host.Client;
}
