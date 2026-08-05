using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Covers the Response.End termination contract (C1-C11, C27-C28 in the plan doc): End completes
// the response as state before unwinding, so code after End does not run, later writes never
// ship, and a swallowing catch cannot un-complete the request. Stage names arrive over the
// witness keyed by the wt token each request carries.
public sealed class ResponseEndOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    private static readonly string[] SkippedStages =
        ["PostRequestHandlerExecute", "ReleaseRequestState", "UpdateRequestCache"];

    private async Task<string[]> StagesAsync(string token)
    {
        var prefix = "stage:" + token + ":";
        return [.. (await scenario.Witness.EventsAsync())
            .Where(e => e.StartsWith(prefix, StringComparison.Ordinal))
            .Select(e => e[prefix.Length..])];
    }

    [Fact]
    public async Task End_Stops_The_Page_And_Skips_To_EndRequest()
    {
        var response = await scenario.Client.GetAsync("/End.aspx?wt=e1");
        var stages = await StagesAsync("e1");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("before-end|");
        response.Header("X-End-Probe").ShouldBe("set");
        stages.ShouldContain("page-finally");
        stages.ShouldContain("page-unload");
        stages.ShouldNotContain("after-end-ran");
        stages.ShouldContain("PreRequestHandlerExecute");
        stages.ShouldContain("EndRequest");
        stages.ShouldContain("LastError-null");
        stages.ShouldAllBe(s => !s.StartsWith("ApplicationError", StringComparison.Ordinal));
        SkippedStages.ShouldAllBe(s => !stages.Contains(s));
    }

    [Fact]
    public async Task Swallowing_Catch_Cannot_Unlock_The_Response()
    {
        var response = await scenario.Client.GetAsync("/EndSwallow.aspx?wt=s1");
        var stages = await StagesAsync("s1");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("before|");
        stages.ShouldContain("swallowed:CancelModuleException");
        stages.ShouldContain("after-second-end");
        stages.ShouldContain("EndRequest");
        stages.ShouldAllBe(s => !s.StartsWith("ApplicationError", StringComparison.Ordinal));
        SkippedStages.ShouldAllBe(s => !stages.Contains(s));
    }

    [Fact]
    public async Task Catch_By_ThreadAbortException_Name_Never_Observes_Termination()
    {
        var response = await scenario.Client.GetAsync("/CatchTae.aspx?wt=t1");
        var stages = await StagesAsync("t1");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("before|");
        stages.ShouldNotContain("tae-caught");
        stages.ShouldContain("EndRequest");
        SkippedStages.ShouldAllBe(s => !stages.Contains(s));
    }

    [Fact]
    public async Task Redirect_Emits_Object_Moved_And_Terminates()
    {
        var response = await scenario.Client.GetAsync("/Redirect.aspx?wt=r1");
        var stages = await StagesAsync("r1");

        response.StatusCode.ShouldBe(302);
        response.Header("Location").ShouldBe("/Default.aspx?value=r");
        response.Text.ShouldBe(
            "<html><head><title>Object moved</title></head><body>\r\n"
            + "<h2>Object moved to <a href=\"/Default.aspx?value=r\">here</a>.</h2>\r\n"
            + "</body></html>\r\n");
        stages.ShouldNotContain("after-redirect");
        stages.ShouldContain("EndRequest");
        stages.ShouldAllBe(s => !s.StartsWith("ApplicationError", StringComparison.Ordinal));
    }

    [Fact]
    public async Task End_In_A_Module_Event_Skips_The_Handler()
    {
        var response = await scenario.Client.GetAsync("/Default.aspx?wt=m1&module-end=1");
        var stages = await StagesAsync("m1");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("module|");
        stages.ShouldContain("BeginRequest");
        stages.ShouldContain("EndRequest");
        stages.ShouldNotContain("PreRequestHandlerExecute");
    }
}
