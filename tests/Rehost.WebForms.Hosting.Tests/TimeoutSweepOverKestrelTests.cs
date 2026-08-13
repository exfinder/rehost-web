using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Guards the cooperative timeout semantics (ledger P53) without waiting on the real timer: the
// probe page runs the sweep with its own request presented as expired. The sweep must flag, not
// abort: the page's own step completes (sweep-returned), and the timeout arrives at the step
// boundary as Framework's error shape. One ApplicationError entry guards the one-shot flag,
// which the POC re-threw at every later boundary.
public sealed class TimeoutSweepOverKestrelTests(SweepLiveScenario scenario)
    : IClassFixture<SweepLiveScenario>
{
    [Fact]
    public async Task Sweep_Delivers_The_Timeout_At_The_Step_Boundary()
    {
        var token = WitnessToken.For(this);
        var response = await scenario.Client.GetAsync("/TimeoutSweep.aspx?" + WitnessToken.Query(token))
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var stages = await scenario.Witness.StagesAsync(token);

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("Request timed out.");
        stages.ShouldContain("sweep-returned");
        stages.ShouldContain("token-canceled:True");
        stages.Count(s => s.StartsWith("ApplicationError:", StringComparison.Ordinal))
            .ShouldBe(1);
        stages.ShouldContain("ApplicationError:HttpException:Request timed out.");
        stages.ShouldContain("EndRequest");
        stages.ShouldContain("LastError-set");
        stages.ShouldNotContain("PostRequestHandlerExecute");
    }

    [Fact]
    public async Task ThreadAbortOnTimeout_False_Suppresses_Delivery_But_Cancels_The_Token()
    {
        var token = WitnessToken.For(this);
        var response = await scenario.Client.GetAsync("/TimeoutSweep.aspx?optout=1&" + WitnessToken.Query(token))
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var stages = await scenario.Witness.StagesAsync(token);

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("sweep-page|");
        stages.ShouldContain("token-canceled:True");
        stages.ShouldAllBe(s => !s.StartsWith("ApplicationError", StringComparison.Ordinal));
        stages.ShouldContain("PostRequestHandlerExecute");
        stages.ShouldContain("LastError-null");
    }
}
