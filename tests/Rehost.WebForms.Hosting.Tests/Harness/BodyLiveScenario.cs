namespace Rehost.WebForms.Hosting.Tests;

// Probes that neither change a host-level limit nor abort their connection share this host.
public sealed class BodyLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.Body);

    internal Uri Address => _host.Address;

    internal ScenarioClient Client => _host.Client;
}
