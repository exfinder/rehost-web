using Shouldly;
using Rehost.WebForms.TestSupport;
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

    [Fact]
    public async Task End_Stops_The_Page_And_Skips_To_EndRequest()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/End.aspx");

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
        var (response, stages) = await scenario.TracedGetAsync(this, "/EndSwallow.aspx");

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
        var (response, stages) = await scenario.TracedGetAsync(this, "/CatchTae.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("before|");
        stages.ShouldNotContain("tae-caught");
        stages.ShouldContain("EndRequest");
        SkippedStages.ShouldAllBe(s => !stages.Contains(s));
    }

    [Fact]
    public async Task Redirect_Emits_Object_Moved_And_Terminates()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/Redirect.aspx");

        response.StatusCode.ShouldBe(302);
        response.Header("Location").ShouldBe(PageRequests.RedirectTarget);
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
        var (response, stages) = await scenario.TracedGetAsync(this, "/Default.aspx?module-end=1");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("module|");
        stages.ShouldContain("BeginRequest");
        stages.ShouldContain("EndRequest");
        stages.ShouldNotContain("PreRequestHandlerExecute");
    }
}
