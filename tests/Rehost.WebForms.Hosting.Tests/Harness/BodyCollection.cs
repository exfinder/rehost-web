using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// A collection rather than a class fixture: the save scenarios read the same application over the
// same limits, and a class fixture would give each test class its own host process.
[CollectionDefinition(nameof(BodyCollection))]
public sealed class BodyCollection
    : ICollectionFixture<BodyLiveScenario>, ICollectionFixture<AbortLiveScenario>;
