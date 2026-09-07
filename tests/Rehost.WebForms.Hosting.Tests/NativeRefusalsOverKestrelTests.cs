using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// IIS answers a missing static file and a request-filtering refusal from a native module:
// no Application_Error, no customErrors, LogRequest onward see the status (MH40-MH42, ledger P97).
public sealed class NativeRefusalsOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task A_Missing_Static_File_Is_A_Response_Not_An_Error()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/nosuch.json");

        response.StatusCode.ShouldBe(404);
        response.Text.ShouldContain("404 - File or directory not found.", Case.Sensitive);
        stages.ShouldContain("BeginRequest");
        stages.ShouldContain("LogRequest:404");
        stages.ShouldContain("LastError-null");
        stages.ShouldNotContain(stage => stage.StartsWith("ApplicationError:"));
    }

    [Fact]
    public async Task A_Hidden_Segment_Is_Refused_Ahead_Of_BeginRequest_Without_An_Error()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/bin/nosuch.dll");

        response.StatusCode.ShouldBe(404);
        response.Text.ShouldContain("404 - File or directory not found.", Case.Sensitive);
        stages.ShouldNotContain("BeginRequest");
        stages.ShouldContain("LogRequest:404");
        stages.ShouldContain("LastError-null");
        stages.ShouldNotContain(stage => stage.StartsWith("ApplicationError:"));
    }

    [Fact]
    public async Task A_Forbidden_Extension_Is_Refused_Ahead_Of_BeginRequest_Without_An_Error()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/web.config");

        response.StatusCode.ShouldBe(404);
        stages.ShouldNotContain("BeginRequest");
        stages.ShouldContain("LogRequest:404");
        stages.ShouldNotContain(stage => stage.StartsWith("ApplicationError:"));
    }

    [Fact]
    public async Task A_Missing_Page_Still_Reaches_Application_Error()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/nosuch.aspx");

        response.StatusCode.ShouldBe(404);
        stages.ShouldContain(stage => stage.StartsWith("ApplicationError:HttpException:"));
    }
}
