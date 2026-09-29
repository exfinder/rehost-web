using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

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

        var (response, stages) = await scenario.TracedGetAsync(this, "/Slow.aspx?ms=5000");

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("Request timed out.", Case.Sensitive);
        stages.ShouldContain("after-sleep-ran");
        stages.ShouldContain("ApplicationError:HttpException:Request timed out.");
        stages.ShouldContain("EndRequest");
        stages.ShouldContain("LastError-set");
        stages.ShouldNotContain("PostRequestHandlerExecute");
    }

    // Framework never aborted a request pending in async: the sweep can only abort an executing
    // thread, and there is none. Measured on 4.8.1 (executionTimeout=1, 17s pending task, past
    // the 15s sweep tick): 200 with the page's own output. The cooperative scan here must make
    // the same distinction — flag sync steps only, never async waits (the P53 deferral).
    [Fact]
    public async Task A_Pending_Async_Task_Is_Never_Timed_Out()
    {
        await scenario.Client.GetAsync("/async/AsyncSlow.aspx?ms=0");

        var response = await scenario.Client.GetAsync("/async/AsyncSlow.aspx")
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("completed[past-budget=True]", Case.Sensitive);
    }
}
