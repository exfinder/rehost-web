namespace Rehost.WebForms.Hosting.Tests;

// Its own application: aspnet:MaxHttpCollectionKeys is per-application and caps every request
// collection, so no other scenario can share a host with it.
public sealed class CollectionKeysLiveScenario : LiveScenario
{
    public CollectionKeysLiveScenario()
        : base(Fixtures.CollectionKeys)
    {
    }
}
