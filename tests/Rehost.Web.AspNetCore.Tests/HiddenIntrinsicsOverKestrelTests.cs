using Rehost.Web.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// IV22: an integrated pool hides HttpContext.Current.Request and .Response during
// Application_Start, the application's own Init() and module Init on a request instance, while a
// classic pool leaves all three readable. The record is written once per activation, so the host
// has to be cold.
public sealed class HiddenIntrinsicsOverKestrelTests
{
    private const string RequestRefusal =
        "HttpException:Request is not available in this context";

    private const string ResponseRefusal =
        "HttpException:Response is not available in this context.";

    [Fact]
    public async Task Application_Start_Init_And_Module_Init_All_Refuse_The_Intrinsics()
    {
        using var scenario = LiveScenario.StartIsolated(
            Fixtures.AppStart, IsolationReason.ColdActivation);

        var report = await scenario.Client.GetAsync(ProbePaths.Lifecycle);

        report.StatusCode.ShouldBe(200);
        report.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).ShouldBe(
        [
            "application-start:current=set;request=" + RequestRefusal
                + ";response=" + ResponseRefusal,
            "module-init:current=set;request=" + RequestRefusal
                + ";response=" + ResponseRefusal,
            "application-init:current=set;request=" + RequestRefusal
                + ";response=" + ResponseRefusal,
        ]);
    }
}
