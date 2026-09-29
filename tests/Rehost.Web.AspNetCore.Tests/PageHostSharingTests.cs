using System.Collections.Concurrent;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// Two classes on the page fixture pin the tenancy contract itself: every class riding
// PageLiveScenario observes the same host process. Under per-class spawning at least one
// observer fails — the first to assert may see only its own entry and pass.
internal static class PageHostSightings
{
    internal static readonly ConcurrentDictionary<string, int> ByClass = new();
}

public sealed class PageHostSharingFirstObserverTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public void Observes_The_Same_Host_As_Every_Other_Page_Class()
    {
        PageHostSightings.ByClass[nameof(PageHostSharingFirstObserverTests)]
            = scenario.HostProcessId;

        PageHostSightings.ByClass.Values.Distinct().Count().ShouldBe(
            1,
            "every class on the page fixture must observe one shared host process");
    }
}

public sealed class PageHostSharingSecondObserverTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public void Observes_The_Same_Host_As_Every_Other_Page_Class()
    {
        PageHostSightings.ByClass[nameof(PageHostSharingSecondObserverTests)]
            = scenario.HostProcessId;

        PageHostSightings.ByClass.Values.Distinct().Count().ShouldBe(
            1,
            "every class on the page fixture must observe one shared host process");
    }
}
