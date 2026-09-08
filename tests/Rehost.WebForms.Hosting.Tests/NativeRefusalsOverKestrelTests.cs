using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// IIS answers a missing static file and a request-filtering refusal from a native module, and
// without runAllManagedModulesForAllRequests no managed event runs for them (MH40-MH42, ledger P97).
public sealed class NativeRefusalsOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task A_Missing_Static_File_Is_A_Response_With_No_Managed_Event()
    {
        var (response, stages) = await scenario.TracedGetAllowingSilenceAsync(this, "/nosuch.json");

        response.StatusCode.ShouldBe(404);
        response.Text.ShouldContain("404 - File or directory not found.", Case.Sensitive);
        stages.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Hidden_Segment_Is_Refused_With_No_Managed_Event()
    {
        var (response, stages) = await scenario.TracedGetAllowingSilenceAsync(this, "/bin/nosuch.dll");

        response.StatusCode.ShouldBe(404);
        response.Text.ShouldContain("404 - File or directory not found.", Case.Sensitive);
        stages.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Forbidden_Extension_Is_Refused_With_No_Managed_Event()
    {
        var (response, stages) = await scenario.TracedGetAllowingSilenceAsync(this, "/web.config");

        response.StatusCode.ShouldBe(404);
        stages.ShouldBeEmpty();
    }

    // The control: the same witness records a managed miss, so the empty lists above are real.
    [Fact]
    public async Task A_Missing_Page_Still_Reaches_Application_Error()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/nosuch.aspx");

        response.StatusCode.ShouldBe(404);
        stages.ShouldContain(stage => stage.StartsWith("ApplicationError:HttpException:"));
        stages.ShouldContain("LastError-set");
    }
}
