using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Activating an application permanently mutates process-global state, so the application is served
// from a child process. This is the only coverage of a dynamically compiled page crossing the
// adapter and a real socket; every other Kestrel assertion here runs precompiled handlers whose
// responses are far smaller than a rendered page.
//
// Both requests share that process. The fixture is written for it: Default.aspx.cs never appends
// to PageProbe.Stages, so a second request renders the same body as the first.
public sealed class PageOverKestrelTests(PageLiveScenario scenario) : IClassFixture<PageLiveScenario>
{
    private const string PageRequest = "/Default.aspx?value=a%26c%20%22q%22%20%C3%A9";

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

    [Fact]
    public async Task Refuses_A_Path_Framework_Maps_To_The_Forbidden_Handler()
    {
        // The page is routed by the shipped root configuration, so this also covers the *.aspx
        // httpHandlers mapping reaching PageHandlerFactory.
        var response = await scenario.Client.GetAsync("/Default.aspx.cs");

        response.StatusCode.ShouldBe(403);
    }
}
