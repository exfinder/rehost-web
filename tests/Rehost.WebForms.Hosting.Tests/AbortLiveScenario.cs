namespace Rehost.WebForms.Hosting.Tests;

// Both aborts kill their socket mid-read, so they never share a connection with anything and can
// share a host with each other, but stay off the main body host.
public sealed class AbortLiveScenario : LiveScenario
{
    public AbortLiveScenario()
        : base(Fixtures.Body)
    {
    }
}
