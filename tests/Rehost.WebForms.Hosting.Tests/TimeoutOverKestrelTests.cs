using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The one test where the real timer chain fires: registration, the scan (2s here via
// rehost:RequestTimeoutScanSeconds), the flag, the boundary checkpoint, and Framework's 500.
// The warm-up request spends the compilation cost so the measured request's budget is all sleep.
public sealed class TimeoutOverKestrelTests(TimeoutLiveScenario scenario)
    : IClassFixture<TimeoutLiveScenario>
{
    [Fact]
    public async Task A_Request_Exceeding_ExecutionTimeout_Renders_Request_Timed_Out()
    {
        await scenario.Client.GetAsync("/Slow.aspx?ms=0");

        var response = await scenario.Client.GetAsync("/Slow.aspx?ms=5000&wt=to1")
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var prefix = "stage:to1:";
        var stages = (await scenario.Witness.EventsAsync())
            .Where(e => e.StartsWith(prefix, StringComparison.Ordinal))
            .Select(e => e[prefix.Length..])
            .ToArray();

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("Request timed out.");
        stages.ShouldContain("after-sleep-ran");
        stages.ShouldContain("ApplicationError:HttpException:Request timed out.");
        stages.ShouldContain("EndRequest");
        stages.ShouldContain("LastError-set");
        stages.ShouldNotContain("PostRequestHandlerExecute");
    }
}
