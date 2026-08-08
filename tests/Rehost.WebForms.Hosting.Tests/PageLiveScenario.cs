namespace Rehost.WebForms.Hosting.Tests;

// Façade over the registry's shared page host: classes keep IClassFixture wiring while every
// class on this fixture reaches the same child process. Disposal belongs to the registry, not
// the class fixture.
public sealed class PageLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.Page);

    internal string ApplicationPath => _host.ApplicationPath;

    internal int HostProcessId => _host.HostProcessId;

    internal Uri Address => _host.Address;

    internal ScenarioClient Client => _host.Client;

    internal WitnessReader Witness => _host.Witness;
}
