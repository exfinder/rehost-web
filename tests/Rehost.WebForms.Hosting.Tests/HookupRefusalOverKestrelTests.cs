using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// IV2: a Global.asax handler naming an event whose add accessor throws was dropped silently on a
// classic pool and rethrown on an integrated one, so this application can never serve.
public sealed class HookupRefusalOverKestrelTests(HookupRefusedLiveScenario scenario)
    : IClassFixture<HookupRefusedLiveScenario>
{
    [Fact]
    public async Task A_Refused_Global_Asax_Subscription_Fails_Every_Request()
    {
        var first = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        var second = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);

        first.StatusCode.ShouldBe(500);
        first.Text.ShouldContain("refused-on-purpose");
        first.Text.ShouldNotContain("<body>scenario");
        second.StatusCode.ShouldBe(500);
        second.Text.ShouldContain("refused-on-purpose");
    }
}
