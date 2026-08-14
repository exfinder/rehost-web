namespace Rehost.WebForms.Hosting.Tests;

// Façade over the registry's shared webserver host, like PageLiveScenario: classes keep
// IClassFixture wiring while every class on this fixture reaches the same child process.
public sealed class WebServerLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.WebServer);

    internal string ApplicationPath => _host.ApplicationPath;

    internal Uri Address => _host.Address;

    internal ScenarioClient Client => _host.Client;
}
