namespace Rehost.WebForms.Hosting.Tests;

// Probes that neither change a host-level limit nor abort their connection share this host.
public sealed class BodyLiveScenario : LiveScenario
{
    public BodyLiveScenario()
        : base(Fixtures.Body)
    {
    }
}
