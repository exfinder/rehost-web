using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The client's cookie container is off, so each test sends the Cookie header it means to send and
// reads the Set-Cookie lines the server actually wrote.
[Collection(nameof(BodyCollection))]
public sealed class CookiesOverKestrelTests(BodyLiveScenario scenario)
{
    // Guards AspNetCoreWorkerRequest.GetKnownRequestHeader(HeaderCookie): System.Web parses the
    // Cookie header itself, so the adapter has to hand it over whole. The subkeys and the valueless
    // cookie are in the same header because a truncated or per-cookie read still answers "a=1".
    [Fact]
    public async Task Reads_The_Cookie_Header_Into_The_Request_Collection()
    {
        var response = await scenario.Client.GetWithCookiesAsync(
            "/cookies?mode=read",
            "a=1; b=x&y=2; c");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("a=1|b=x&y=2{:x,y:2}|c=");
    }

    // Guards the response side of the same seam: ResponseSpool keeps every header the pipeline
    // sends as its own entry and the commit appends rather than assigns, so two cookies leave as
    // two Set-Cookie lines. Joining them into one header, or letting the last win, is what a
    // dictionary-shaped response would do.
    [Fact]
    public async Task Emits_One_Set_Cookie_Line_Per_Response_Cookie()
    {
        var response = await scenario.Client.GetAsync("/cookies?mode=pair");

        response.SetCookies.ShouldBe(["first=1; path=/", "second=2; path=/"]);
    }

    // The attribute text is Framework's, including the pre-RFC dashed expires format and the
    // lower-case attribute names. It carries a comma, which is exactly what a joined header
    // collection would make ambiguous. HttpOnly is written only when Request.Browser answers, so
    // this line also depends on the shipped browserCaps result substitution.
    [Fact]
    public async Task Renders_Cookie_Attributes_As_Framework_Writes_Them()
    {
        var response = await scenario.Client.GetAsync("/cookies?mode=attributes");

        response.SetCookies.ShouldBe([
            "marked=value; domain=example.test; expires=Tue, 02-Jan-2035 03:04:05 GMT"
            + "; path=/scoped; secure; HttpOnly; SameSite=Lax",
        ]);
    }

    // Re-issuing the cookie that arrived is a common way to extend one, and it is the only path on
    // which the defaults a request cookie is born with become visible. A cookie born with the field
    // defaults instead of the configured ones carries SameSite None, which .NET Framework 4.8.1
    // does not emit here — and which a browser drops outright without Secure.
    [Fact]
    public async Task Re_Issuing_A_Received_Cookie_Adds_No_Same_Site_Attribute()
    {
        var response = await scenario.Client.GetWithCookiesAsync("/cookies?mode=reissue", "a=1");

        response.SetCookies.ShouldBe(["a=1; path=/"]);
    }

    // Framework sends response headers as HeaderEncoding bytes (UTF-8 by default); the adapter
    // configures Kestrel to match. Raw bytes, because HttpClient's Latin-1 header decode would
    // disguise the wire form.
    [Fact]
    public async Task Encodes_A_Non_Ascii_Cookie_Value_As_Utf8_Header_Bytes()
    {
        var response = await scenario.Client.GetAsync("/cookies?mode=unicode");
        response.StatusCode.ShouldBe(200);

        var raw = await RawSocketProbe.GetRawResponseAsync(
            scenario.Address, "/cookies?mode=unicode");
        var setCookie = "place=caf\u00e9"u8.ToArray();
        raw.AsSpan().IndexOf(setCookie).ShouldBeGreaterThanOrEqualTo(
            0, "the UTF-8 bytes of the cookie value must appear in the Set-Cookie line");
    }
}
