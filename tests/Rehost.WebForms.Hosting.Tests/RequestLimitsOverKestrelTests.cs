using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The fixture's <requestLimits> and <verbs> over the shipped baseline's numbers: what request
// filtering refused before any managed code ran, and what it let through.
public sealed class RequestLimitsOverKestrelTests(WebServerLiveScenario scenario)
    : IClassFixture<WebServerLiveScenario>
{
    [Fact]
    public async Task A_Declared_Length_Over_The_Limit_Is_Refused_And_The_Connection_Closed()
    {
        var refused = await PostRawAsync(1500);
        var (headers, body) = RawResponse.Split(refused);
        var allowed = await scenario.Client.PostBodyAsync("/Default.aspx", Body(500));
        var chunked = await scenario.Client.PostBodyAsync(
            "/Default.aspx", Body(1500), BodyFraming.Chunked);

        headers.ShouldStartWith("HTTP/1.1 413 ", Case.Sensitive);
        headers.ShouldContain("Connection: close", Case.Sensitive);
        body.ShouldContain("413 - Request entity too large.", Case.Sensitive);
        allowed.StatusCode.ShouldBe(200);
        chunked.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task A_Url_Over_The_Limit_Is_Refused_Where_Path_Info_Past_A_Page_Is_Not()
    {
        var refused = await scenario.Client.GetAsync($"/{new string('u', 300)}");
        var pathInfo = await scenario.Client.GetAsync($"/rw-probe.aspx/{new string('p', 300)}");

        refused.StatusCode.ShouldBe(404);
        refused.Text.ShouldContain("404.14 URL Too Long", Case.Sensitive);
        pathInfo.StatusCode.ShouldBe(200);
        pathInfo.Text.ShouldContain($"pathinfo=/{new string('p', 300)}", Case.Sensitive);
    }

    [Fact]
    public async Task A_Query_Over_The_Limit_Is_Refused_By_Its_Raw_Bytes()
    {
        var allowed = await scenario.Client.GetAsync($"/Default.aspx?q={new string('q', 198)}");
        var refused = await scenario.Client.GetAsync($"/Default.aspx?q={new string('q', 199)}");

        allowed.StatusCode.ShouldBe(200);
        refused.StatusCode.ShouldBe(404);
        refused.Text.ShouldContain("404.15 Query String Too Long", Case.Sensitive);
    }

    [Fact]
    public async Task A_Denied_Verb_Is_Refused_Where_An_Unlisted_One_Serves()
    {
        var refused = await scenario.Client.DeleteAsync("/Default.aspx");
        var refusedBeforeRewrite = await scenario.Client.DeleteAsync("/rw/clean/5");
        var allowed = await scenario.Client.PostFormAsync("/Default.aspx", "a=1");

        refused.StatusCode.ShouldBe(404);
        refused.Text.ShouldContain("404.6 Verb Denied", Case.Sensitive);
        refused.Header("Connection").ShouldBe("close");
        refusedBeforeRewrite.StatusCode.ShouldBe(404);
        refusedBeforeRewrite.Text.ShouldContain("404.6 Verb Denied", Case.Sensitive);
        allowed.StatusCode.ShouldBe(200);
    }

    private static byte[] Body(int length) =>
        Encoding.ASCII.GetBytes(new string('b', length));

    // A declared length the server refuses before reading it: the client must not send
    // Connection: close itself, or the answer's own close header proves nothing.
    private Task<byte[]> PostRawAsync(int length)
    {
        var request = $"""
            POST /Default.aspx HTTP/1.1
            Host: {scenario.Address.Authority}
            Content-Type: text/plain
            Content-Length: {length}

            {new string('b', length)}
            """.ReplaceLineEndings("\r\n");

        return RawSocketProbe.SendRawAsync(scenario.Address, Encoding.ASCII.GetBytes(request));
    }
}
