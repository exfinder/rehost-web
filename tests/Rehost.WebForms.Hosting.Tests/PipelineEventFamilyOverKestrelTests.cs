using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The three events an integrated pool raises and a classic one refused (IV1-IV5, IV17):
// MapRequestHandler right after handler mapping, LogRequest and PostLogRequest after
// PostUpdateRequestCache and ahead of EndRequest, including after every early end.
public sealed class PipelineEventFamilyOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    private const string LogRequestPrefix = "LogRequest:";

    [Fact]
    public async Task The_Three_Events_Take_Their_Integrated_Positions()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/Default.aspx");

        response.StatusCode.ShouldBe(200);
        Index(stages, "BeginRequest").ShouldBeLessThan(Index(stages, "MapRequestHandler"));
        Index(stages, "MapRequestHandler")
            .ShouldBeLessThan(Index(stages, "PreRequestHandlerExecute"));
        Index(stages, "UpdateRequestCache").ShouldBeLessThan(LogIndex(stages));
        LogIndex(stages).ShouldBeLessThan(Index(stages, "PostLogRequest"));
        Index(stages, "PostLogRequest").ShouldBeLessThan(Index(stages, "EndRequest"));
    }

    [Fact]
    public async Task The_Response_Is_Still_Open_At_LogRequest()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/Default.aspx?lw=1");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldEndWith("|log");
        response.Header("X-At-LogRequest").ShouldBe("1");
        stages.ShouldAllBe(s => !s.StartsWith("lw:threw", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_Early_End_From_The_Page_Still_Logs_Before_EndRequest()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/End.aspx");

        response.StatusCode.ShouldBe(200);
        stages.ShouldNotContain("UpdateRequestCache");
        LogIndex(stages).ShouldBeLessThan(Index(stages, "PostLogRequest"));
        Index(stages, "PostLogRequest").ShouldBeLessThan(Index(stages, "EndRequest"));
    }

    [Fact]
    public async Task A_Terminating_Redirect_Logs_The_Redirect_Status_Before_EndRequest()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/Redirect.aspx");

        response.StatusCode.ShouldBe(302);
        stages.ShouldNotContain("after-redirect");
        stages.ShouldNotContain("PostRequestHandlerExecute");
        stages.ShouldNotContain("UpdateRequestCache");
        stages.ShouldContain(LogRequestPrefix + "302");
        LogIndex(stages).ShouldBeLessThan(Index(stages, "PostLogRequest"));
        Index(stages, "PostLogRequest").ShouldBeLessThan(Index(stages, "EndRequest"));
    }

    [Fact]
    public async Task An_Early_End_At_BeginRequest_Logs_Without_Reaching_The_Map_Step()
    {
        var (response, stages) =
            await scenario.TracedGetAsync(this, "/Default.aspx?module-end=1");

        response.StatusCode.ShouldBe(200);
        stages.ShouldNotContain("MapRequestHandler");
        LogIndex(stages).ShouldBeLessThan(Index(stages, "EndRequest"));
    }

    // IV17/IV21: the Error event still sees 200, and LogRequest onward sees the 500 the rendered
    // error page carries.
    [Fact]
    public async Task An_Unhandled_Page_Error_Logs_Between_The_Error_Event_And_EndRequest()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/xfer/Boom.aspx");

        response.StatusCode.ShouldBe(500);
        Index(stages, s => s.StartsWith("ApplicationError:", StringComparison.Ordinal))
            .ShouldBeLessThan(LogIndex(stages));
        LogIndex(stages).ShouldBeLessThan(Index(stages, "EndRequest"));
        stages.ShouldContain(LogRequestPrefix + "500");
    }

    [Fact]
    public async Task A_Remap_At_MapRequestHandler_Is_Refused()
    {
        var (response, stages) =
            await scenario.TracedGetAsync(this, "/Default.aspx?remap=late");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldNotContain("REMAPPED");
        stages.ShouldContain("remap:threw:InvalidOperationException");
    }

    [Fact]
    public async Task A_Remap_Before_The_Map_Step_Still_Chooses_The_Handler()
    {
        var (response, stages) =
            await scenario.TracedGetAsync(this, "/Default.aspx?remap=early");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("REMAPPED");
        stages.ShouldContain("remap:ok");
    }

    private static int LogIndex(string[] stages) =>
        Index(stages, s => s.StartsWith(LogRequestPrefix, StringComparison.Ordinal));

    private static int Index(string[] stages, string stage) =>
        Index(stages, s => s == stage);

    private static int Index(string[] stages, Func<string, bool> match)
    {
        var index = Array.FindIndex(stages, new Predicate<string>(match));
        return index >= 0
            ? index
            : throw new InvalidOperationException(
                "No stage matched among " + string.Join(", ", stages) + ".");
    }
}
