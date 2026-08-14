namespace Rehost.WebForms.Hosting.Tests;

// Both aborts kill their socket mid-read, so they never share a connection with anything and can
// share a host with each other, but stay off the main body host — hence the registry role.
public sealed class AbortLiveScenario(ScenarioHostRegistry registry)
{
    private readonly LiveScenario _host = registry.GetOrAdd(Fixtures.Body, role: "abort");

    internal Uri Address => _host.Address;

    internal HostWitness Witness => _host.Witness;
}
