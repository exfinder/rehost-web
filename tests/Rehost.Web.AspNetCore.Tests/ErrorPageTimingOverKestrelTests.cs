using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// IV21: integrated renders the error page and sets the status before LogRequest, so the Log and
// EndRequest handlers see a complete 500 they can append to or clear. Classic rendered after
// EndRequest and discarded whatever those handlers wrote.
public sealed class ErrorPageTimingOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task The_Error_Page_Is_Already_Buffered_When_The_Log_Handlers_Write()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/xfer/Boom.aspx?err=append");

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("boom-from-page");
        response.Text.ShouldEndWith("[Log][PLog][End]", Case.Sensitive);
        response.Text.IndexOf("boom-from-page", StringComparison.Ordinal)
            .ShouldBeLessThan(response.Text.IndexOf("[Log]", StringComparison.Ordinal));
        stages.ShouldContain("err|Log=500");
        stages.ShouldContain("err|End=500");
    }

    [Fact]
    public async Task A_Clear_At_LogRequest_Leaves_Only_What_The_Handlers_Wrote()
    {
        var response = await scenario.Client.GetAsync("/xfer/Boom.aspx?err=clear");

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldBe("[Log][PLog][End]");
        response.Header("Content-Length").ShouldBe("16");
    }

    [Fact]
    public async Task The_Error_Event_Still_Sees_The_Status_Before_The_Render()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/xfer/Boom.aspx?err=append");

        var raised = Array.FindIndex(
            stages, stage => stage.StartsWith("ApplicationError:", StringComparison.Ordinal));

        response.StatusCode.ShouldBe(500);
        raised.ShouldBeGreaterThanOrEqualTo(0);
        stages[raised].ShouldEndWith(";status=200");
        raised.ShouldBeLessThan(Array.IndexOf(stages, "err|Log=500"));
    }

    // Case.Sensitive is load-bearing: Shouldly's string comparisons default to insensitive,
    // which is blind to exactly the upper-casing filter this measures.
    [Fact]
    public async Task An_Installed_Response_Filter_Does_Not_Reach_The_Error_Page()
    {
        var response = await scenario.Client.GetAsync("/xfer/Boom.aspx?filter=1&err=append");

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("boom-from-page", Case.Sensitive);
        response.Text.ShouldEndWith("[Log][PLog][End]", Case.Sensitive);
    }

    [Fact]
    public async Task The_Same_Filter_Upper_Cases_A_Response_That_Does_Not_Fail()
    {
        var response = await scenario.Client.GetAsync("/xfer/Boom.aspx?filter=1&nothrow=1");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("PAGE-OUTPUT", Case.Sensitive);
    }
}
