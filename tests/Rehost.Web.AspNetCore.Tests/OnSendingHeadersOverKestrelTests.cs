using Rehost.Web.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// IV11: the pre-send callback queue fires at the head-commit boundary with the status still 200,
// and refuses a registration once the head has left.
public sealed class OnSendingHeadersOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task A_Callback_Amends_The_Head_Before_It_Is_Emitted()
    {
        var response = await scenario.Client.GetAsync(ProbePaths.OnSendingHeaders);

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("registered");
        response.Header("X-On-Sending").ShouldBe("200");
    }

    [Fact]
    public async Task A_Registration_After_The_Head_Has_Left_Is_Refused()
    {
        var response = await scenario.Client.GetAsync(
            ProbePaths.OnSendingHeaders + "?mode=late");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("late:HttpException");
        response.Header("X-On-Sending").ShouldBeNull();
    }
}
