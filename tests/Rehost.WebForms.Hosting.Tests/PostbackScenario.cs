using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// All the probes share one host process: they share one fixture, so they are one application, and
// none of them claims anything about cold activation. Spawning per test paid a full activation and
// page compilation — about 1.7s — to serve two requests that cost nothing.
public sealed class PostbackScenario : IDisposable
{
    internal ScenarioRun Run { get; } = ScenarioRun.Postback(
        "apply",
        "apply-twice",
        "bump",
        "tamper",
        "cross-page",
        "unsafe-input",
        "upload",
        "upload-empty",
        "upload-none");

    public void Dispose()
    {
        Run.Dispose();
    }
}

[CollectionDefinition(nameof(PostbackCollection))]
public sealed class PostbackCollection : ICollectionFixture<PostbackScenario>;
