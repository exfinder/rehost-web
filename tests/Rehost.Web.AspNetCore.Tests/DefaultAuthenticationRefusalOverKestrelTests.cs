using Rehost.Web.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

public sealed class DefaultAuthenticationRefusalOverKestrelTests(
    DefaultAuthRefusedLiveScenario scenario)
    : IClassFixture<DefaultAuthRefusedLiveScenario>
{
    [Fact]
    public async Task A_Global_Asax_DefaultAuthentication_Handler_Fails_Every_Request()
    {
        var first = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        var second = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);

        first.StatusCode.ShouldBe(500);
        first.Text.ShouldContain("DefaultAuthentication.Authenticate", Case.Sensitive);
        first.Text.ShouldNotContain("<body>scenario");
        second.StatusCode.ShouldBe(500);
        second.Text.ShouldContain("DefaultAuthentication.Authenticate", Case.Sensitive);
    }
}
