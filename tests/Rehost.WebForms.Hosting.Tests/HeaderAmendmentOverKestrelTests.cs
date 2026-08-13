using Shouldly;
using Rehost.WebForms.TestSupport;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Guards the reopened header window after Response.End (ledger P55, readings R19-R24): nothing
// reaches the wire before the single commit, so a header or cookie stamped in EndRequest still
// ships after End and a terminating Redirect, as Framework's abort arm delivered. The two green
// edges pin the fences: CompleteRequest was already open, and an application's own Flush still
// seals (R24: the append throws and is lost).
public sealed class HeaderAmendmentOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task EndRequest_Header_And_Cookie_Survive_Response_End()
    {
        var token = WitnessToken.For(this);
        var response = await scenario.Client.GetAsync(
            "/Amend.aspx?mode=end&stamp=1&" + WitnessToken.Query(token));
        var stages = await scenario.Witness.StagesAsync(token);

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("amend-start|");
        response.Header("X-After-End").ShouldBe("stamped");
        response.SetCookies.ShouldContain("late=yes; path=/");
        stages.ShouldContain("stamp:append-ok|cookie-ok");
    }

    [Fact]
    public async Task EndRequest_Header_Survives_A_Terminating_Redirect()
    {
        var token = WitnessToken.For(this);
        var response = await scenario.Client.GetAsync(
            "/Amend.aspx?mode=redirect&stamp=1&" + WitnessToken.Query(token));
        var stages = await scenario.Witness.StagesAsync(token);

        response.StatusCode.ShouldBe(302);
        response.Header("Location").ShouldBe(PageRequests.RedirectTarget);
        response.Header("X-After-End").ShouldBe("stamped");
        response.SetCookies.ShouldContain("late=yes; path=/");
        stages.ShouldContain("stamp:append-ok|cookie-ok");
    }

    [Fact]
    public async Task EndRequest_Header_Survives_CompleteRequest()
    {
        var token = WitnessToken.For(this);
        var response = await scenario.Client.GetAsync(
            "/Amend.aspx?mode=complete&stamp=1&" + WitnessToken.Query(token));
        var stages = await scenario.Witness.StagesAsync(token);

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("amend-start|amend-tail|");
        response.Header("X-After-End").ShouldBe("stamped");
        stages.ShouldContain("stamp:append-ok|cookie-ok");
    }

    private static (string Headers, string Body) SplitRaw(byte[] raw)
    {
        var text = System.Text.Encoding.Latin1.GetString(raw);
        var boundary = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        return (text[..boundary], text[(boundary + 4)..]);
    }

    [Fact]
    public async Task An_Ended_Response_States_Its_Exact_Content_Length()
    {
        var raw = await RawSocketProbe.GetRawResponseAsync(
            scenario.Address,
            "/Amend.aspx?mode=end&stamp=1&" + WitnessToken.Query(WitnessToken.For(this)));
        var (headers, body) = SplitRaw(raw);

        headers.ShouldStartWith("HTTP/1.1 200");
        body.ShouldBe("amend-start|");
        headers.ShouldContain("Content-Length: 12");
        headers.ShouldNotContain("Transfer-Encoding");
        headers.ShouldContain("X-After-End: stamped");
    }

    [Fact]
    public async Task A_Terminating_Redirect_States_Its_Exact_Content_Length()
    {
        var raw = await RawSocketProbe.GetRawResponseAsync(
            scenario.Address,
            "/Amend.aspx?mode=redirect&stamp=1&" + WitnessToken.Query(WitnessToken.For(this)));
        var (headers, body) = SplitRaw(raw);

        headers.ShouldStartWith("HTTP/1.1 302");
        headers.ShouldContain("Content-Length: " + body.Length);
        headers.ShouldNotContain("Transfer-Encoding");
        headers.ShouldContain("X-After-End: stamped");
    }

    // Reading W5: an explicit Flush in EndRequest after End writes the headers immediately with
    // the amendments so far, forfeits the length, and seals against later appends.
    [Fact]
    public async Task A_Flush_After_End_In_EndRequest_Seals_And_Forfeits_The_Length()
    {
        var token = WitnessToken.For(this);
        var raw = await RawSocketProbe.GetRawResponseAsync(
            scenario.Address, "/Amend.aspx?mode=end&stamp=1&fae=1&" + WitnessToken.Query(token));
        var (headers, body) = SplitRaw(raw);
        var stages = await scenario.Witness.StagesAsync(token);

        headers.ShouldStartWith("HTTP/1.1 200");
        body.ShouldContain("amend-start|");
        headers.ShouldNotContain("Content-Length");
        headers.ShouldContain("X-After-End: stamped");
        headers.ShouldNotContain("X-Late-2");
        stages.ShouldContain("fae:flush-ok|late2-threw:HttpException");
    }

    [Fact]
    public async Task An_Application_Flush_Still_Seals_The_Headers()
    {
        var token = WitnessToken.For(this);
        var response = await scenario.Client.GetAsync(
            "/Amend.aspx?mode=flush&stamp=1&" + WitnessToken.Query(token));
        var stages = await scenario.Witness.StagesAsync(token);

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("amend-start|amend-tail|");
        response.Header("X-After-End").ShouldBeNull();
        stages.ShouldContain(s => s.StartsWith("stamp:append-threw:HttpException", StringComparison.Ordinal));
    }
}
