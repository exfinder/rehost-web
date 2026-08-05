using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The postback and multipart tests share one host process: they share one fixture, so they are one
// application, and none of them claims anything about cold activation. Spawning per test paid a
// full activation and page compilation — about 1.7s — to serve two requests that cost nothing.
public sealed class PostbackScenario : LiveScenario
{
    public PostbackScenario()
        : base(Fixtures.Postback)
    {
    }
}

[CollectionDefinition(nameof(PostbackCollection))]
public sealed class PostbackCollection : ICollectionFixture<PostbackScenario>;
