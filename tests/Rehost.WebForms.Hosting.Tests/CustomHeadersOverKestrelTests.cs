using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The fixture's <customHeaders> over the shipped baseline's inherited X-Powered-By row: what rides
// which responses (CH1-CH4), and what the application can do about it (CH5, CH6, CH8).
public sealed class CustomHeadersOverKestrelTests(WebServerLiveScenario scenario)
    : IClassFixture<WebServerLiveScenario>
{
    [Fact]
    public async Task Every_Response_The_Host_Produces_Carries_The_Configured_Rows()
    {
        var page = await scenario.Client.GetAsync("/Default.aspx");
        var staticFile = await scenario.Client.GetAsync("/data.probe");
        var missing = await scenario.Client.GetAsync("/nosuch.txt");
        var redirect = await scenario.Client.GetAsync("/rw/old");

        page.StatusCode.ShouldBe(200);
        staticFile.StatusCode.ShouldBe(200);
        missing.StatusCode.ShouldBe(404);
        redirect.StatusCode.ShouldBe(301);
        foreach (var response in new[] { page, staticFile, missing, redirect })
        {
            response.Header("X-Powered-By").ShouldBe("ASP.NET");
            response.Header("X-Fixture").ShouldBe("honored");
        }
    }

    [Fact]
    public async Task The_Inherited_Row_Is_Written_Before_The_Rows_The_Application_Adds()
    {
        var (raw, _) = await scenario.TracedRawGetAsync(this, "/Default.aspx");
        var head = Head(raw);

        head.IndexOf("X-Powered-By: ASP.NET", StringComparison.Ordinal)
            .ShouldBeLessThan(head.IndexOf("X-Fixture: honored", StringComparison.Ordinal));
    }

    // Read off the wire: HttpClient parses Cache-Control and re-serializes its directives in a
    // canonical order, which hides whether the line was joined or duplicated.
    [Fact]
    public async Task Cache_Control_Joins_The_Response_Word_Instead_Of_Duplicating()
    {
        var page = await RawHead("/Default.aspx");
        var staticFile = await RawHead("/data.probe");

        page.ShouldContain("Cache-Control: private,no-store\r\n", Case.Sensitive);
        page.Split("Cache-Control:").Length.ShouldBe(2, page);
        staticFile.ShouldContain("Cache-Control: no-store\r\n", Case.Sensitive);
    }

    [Fact]
    public async Task An_Application_Header_Of_The_Same_Name_Is_A_Second_Line_Behind_It()
    {
        var (raw, _) = await scenario.TracedRawGetAsync(
            this, ProbePaths.CustomHeaders + "?case=append");

        Head(raw).ShouldContain("X-Fixture: app\r\nX-Fixture: honored", Case.Sensitive);
    }

    [Fact]
    public async Task An_Application_Cannot_Remove_A_Configured_Row()
    {
        var response = await scenario.Client.GetAsync(ProbePaths.CustomHeaders + "?case=remove");

        response.Text.ShouldBe("custom-headers");
        response.Header("X-Fixture").ShouldBe("honored");
    }

    private async Task<string> RawHead(string path) =>
        Head(await RawSocketProbe.GetRawResponseAsync(scenario.Address, path));

    private static string Head(byte[] raw) => RawResponse.Split(raw).Headers;
}
