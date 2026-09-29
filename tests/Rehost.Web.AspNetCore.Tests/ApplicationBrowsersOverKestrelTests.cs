using Rehost.Web.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// Expected values are Framework's except RehostHyphen and RehostUnderscore: Framework's culture sort
// put bw.browser before b-x.browser and a_b.browser before ab.browser; directory order does not.
public sealed class ApplicationBrowsersOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Theory]
    [InlineData("RehostOrder/1.0", "default,orderalpha", "OrderAlpha")]
    [InlineData("RehostHyphen/1.0", "default,hyphenbx", "HyphenBX")]
    [InlineData("RehostUnderscore/1.0", "default,plainab", "PlainAB")]
    [InlineData("RehostDot/1.0", "default,dotfile", "DotFile")]
    [InlineData("RehostUpper/1.0", "default,upperext", "UpperExt")]
    [InlineData("RehostSub/1.0", "default,subroot", "SubRoot")]
    [InlineData("RehostSub/1.0 RehostSubUpper/1.0", "default,subroot,subupper", "SubUpper")]
    [InlineData("RehostSub/1.0 RehostSubHidden/1.0", "default,subroot,subhidden", "SubHidden")]
    [InlineData("RehostSub/1.0 RehostSubDot/1.0", "default,subroot,subdotfile", "SubDotFile")]
    [InlineData("RehostTree/1.0", "default,treealpha,treebeta", "TreeBeta")]
    [InlineData("curl/8.4.0", "default", "none")]
    public async Task An_Application_Browser_File_Identifies_Its_User_Agent(
        string userAgent, string browsers, string probe)
    {
        var response = await scenario.Client.GetWithHeadersAsync(
            ProbePaths.BrowserCapabilities, ("User-Agent", userAgent));

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldBe($"browsers={browsers}\nprobe={probe}\n");
    }

    [Fact]
    public async Task A_Request_Without_A_User_Agent_Is_The_Default_Browser()
    {
        var response = await scenario.Client.GetAsync(ProbePaths.BrowserCapabilities);

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldBe("browsers=default\nprobe=none\n");
    }
}
