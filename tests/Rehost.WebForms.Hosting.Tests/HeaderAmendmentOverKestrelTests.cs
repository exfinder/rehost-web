using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Guards the reopened header window after Response.End (ledger P55, readings R19-R24): nothing
// reaches the wire before the single commit, so a header or cookie stamped in EndRequest still
// ships after End and a terminating Redirect, as Framework's abort arm delivered. The two green
// edges pin the fences: CompleteRequest was already open, and an application's own Flush still
// seals (R24: the append throws and is lost).
public sealed class HeaderAmendmentOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    private async Task<string[]> StagesAsync(string token)
    {
        var prefix = "stage:" + token + ":";
        return [.. (await scenario.Witness.EventsAsync())
            .Where(e => e.StartsWith(prefix, StringComparison.Ordinal))
            .Select(e => e[prefix.Length..])];
    }

    [Fact]
    public async Task EndRequest_Header_And_Cookie_Survive_Response_End()
    {
        var response = await scenario.Client.GetAsync("/Amend.aspx?mode=end&stamp=1&wt=a1");
        var stages = await StagesAsync("a1");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("amend-start|");
        response.Header("X-After-End").ShouldBe("stamped");
        response.SetCookies.ShouldContain("late=yes; path=/");
        stages.ShouldContain("stamp:append-ok|cookie-ok");
    }

    [Fact]
    public async Task EndRequest_Header_Survives_A_Terminating_Redirect()
    {
        var response = await scenario.Client.GetAsync("/Amend.aspx?mode=redirect&stamp=1&wt=a2");
        var stages = await StagesAsync("a2");

        response.StatusCode.ShouldBe(302);
        response.Header("Location").ShouldBe("/Default.aspx?value=r");
        response.Header("X-After-End").ShouldBe("stamped");
        response.SetCookies.ShouldContain("late=yes; path=/");
        stages.ShouldContain("stamp:append-ok|cookie-ok");
    }

    [Fact]
    public async Task EndRequest_Header_Survives_CompleteRequest()
    {
        var response = await scenario.Client.GetAsync("/Amend.aspx?mode=complete&stamp=1&wt=a3");
        var stages = await StagesAsync("a3");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("amend-start|amend-tail|");
        response.Header("X-After-End").ShouldBe("stamped");
        stages.ShouldContain("stamp:append-ok|cookie-ok");
    }

    [Fact]
    public async Task An_Application_Flush_Still_Seals_The_Headers()
    {
        var response = await scenario.Client.GetAsync("/Amend.aspx?mode=flush&stamp=1&wt=a4");
        var stages = await StagesAsync("a4");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("amend-start|amend-tail|");
        response.Header("X-After-End").ShouldBeNull();
        stages.ShouldContain(s => s.StartsWith("stamp:append-threw:HttpException", StringComparison.Ordinal));
    }
}
