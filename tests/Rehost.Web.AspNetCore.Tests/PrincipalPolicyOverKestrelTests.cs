using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

public sealed class PrincipalPolicyOverKestrelTests(PrincipalPolicyLiveScenario scenario)
    : IClassFixture<PrincipalPolicyLiveScenario>
{
    [Fact]
    public async Task Requests_Keep_HttpContext_User_After_The_Windows_Principal_Policy_Is_Set()
    {
        var before = await scenario.Client.GetAsync("/PrincipalPolicy.aspx");
        var set = await scenario.Client.GetAsync("/PrincipalPolicy.aspx?set=1");
        var after = await scenario.Client.GetAsync("/PrincipalPolicy.aspx");

        before.StatusCode.ShouldBe(200);
        before.Text.ShouldBe("user=False/|thread=False/|");
        set.StatusCode.ShouldBe(200);
        set.Text.ShouldBe("policy-set|");
        after.StatusCode.ShouldBe(200);
        after.Text.ShouldBe(before.Text);
    }
}
