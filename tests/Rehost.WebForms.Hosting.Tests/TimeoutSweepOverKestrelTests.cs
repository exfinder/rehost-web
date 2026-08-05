using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Guards the neutralized RequestTimeoutManager sweep (ledger P51): the probe page runs the sweep
// with its own request looking expired. A sweep that still reaches Thread.Abort throws
// PlatformNotSupportedException and the marker never renders.
public sealed class TimeoutSweepOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task Sweeping_An_Expired_Request_Leaves_It_Running()
    {
        var response = await scenario.Client.GetAsync("/TimeoutSweep.aspx")
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("sweep-survived");
    }
}
