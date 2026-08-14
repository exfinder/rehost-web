namespace Rehost.WebForms.Hosting.Tests;

// Façade over the registry's shared postback host: the postback and multipart tests are one
// application and none of them claims anything about cold activation, so they reach one child
// process. Spawning per test paid a full activation and page compilation — about 1.7s — to
// serve two requests that cost nothing.
public sealed class PostbackLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.Postback);

    internal Uri Address => _host.Address;

    internal ScenarioClient Client => _host.Client;
}
