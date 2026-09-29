using Shouldly;
using Rehost.Web.TestSupport;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// Activating an application permanently mutates process-global state, so the application is served
// from a child process. This is the only coverage of a dynamically compiled page crossing the
// adapter and a real socket; every other Kestrel assertion here runs precompiled handlers whose
// responses are far smaller than a rendered page.
//
// Both requests share that process. The fixture is written for it: Default.aspx.cs never appends
// to PageProbe.Stages, so a second request renders the same body as the first.
public sealed class PageOverKestrelTests(PageLiveScenario scenario) : IClassFixture<PageLiveScenario>
{
    private const string PageRequest = PageRequests.Canonical;

    [Fact]
    public async Task Serves_A_Dynamically_Compiled_Page_Over_Kestrel()
    {
        var response = await scenario.Client.GetAsync(PageRequest);

        response.StatusCode.ShouldBe(200);
        response.ContentType.ShouldBe("text/html; charset=utf-8");
        response.Bytes.ShouldBe(File.ReadAllBytes(
            Path.Combine(scenario.ApplicationPath, "Default.expected.html")));
    }

    [Fact]
    public async Task A_Page_Naming_HttpUtility_Compiles_Against_The_Port_Alone()
    {
        // The shared framework's System.Web.HttpUtility.dll is excluded from the codegen
        // reference set; if it returns, this page fails CS0433 rather than rendering.
        var response = await scenario.Client.GetAsync("/Encode.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("a&lt;b &amp; &quot;c&quot;");
    }

    [Fact]
    public async Task Serves_A_Static_File_Through_StaticFileHandler()
    {
        // A doubled application root survives GetFileInfo (request.PhysicalPath is correct) and
        // fails only inside TransmitFile, so asserting the bytes is what catches ledger P54.
        var response = await scenario.Client.GetAsync("/styles.css");

        response.StatusCode.ShouldBe(200);
        response.ContentType.ShouldBe("text/css");
        response.Bytes.ShouldBe(File.ReadAllBytes(
            Path.Combine(scenario.ApplicationPath, "styles.css")));
    }

    [Fact]
    public async Task Reports_A_Missing_Static_File_As_Not_Found()
    {
        var response = await scenario.Client.GetAsync("/missing.css");

        response.StatusCode.ShouldBe(404);
    }

    // The code-behind is on disk and its extension is on IIS request filtering's deny list, so
    // the file exists and the answer is still 404 — the shape IIS gave it (404.7), where the
    // retired classic <httpHandlers> table gave System.Web's 403.
    [Fact]
    public async Task Refuses_A_Path_On_The_Request_Filtering_Deny_List()
    {
        var response = await scenario.Client.GetAsync("/Default.aspx.cs");

        response.StatusCode.ShouldBe(404);
        response.Text.ShouldNotContain("class");
    }
}
