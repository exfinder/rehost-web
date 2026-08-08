namespace Rehost.WebForms.Hosting.Tests;

// Dedicated host over the page payload: the sweep probe presents every registered request as
// expired, so any other class's in-flight request on the same host would be spuriously timed
// out. Never route this through the shared registry.
public sealed class SweepLiveScenario : LiveScenario
{
    public SweepLiveScenario()
        : base(Fixtures.Page)
    {
    }
}
