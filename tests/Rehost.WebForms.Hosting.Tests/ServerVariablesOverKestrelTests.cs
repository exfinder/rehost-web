using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// What Request.Url, IsSecureConnection, and the server variables report on a live host, against
// the IIS + Framework reading (R1–R5, research/host-adapter-residuals.md): the Host header names
// the server, forwarded headers from a trusted (loopback) proxy restore the client's scheme and
// host, request-header bytes decode as IIS decoded them, and response-header bytes leave as
// Framework's default encoding wrote them.
public sealed class ServerVariablesOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    // The header block goes on the wire in the given encoding: Latin-1 puts a lone \xE9 or an
    // \xFF\xFE pair into the bytes exactly, UTF-8 the multi-byte sequences.
    private async Task<(Dictionary<string, string> Lines, byte[] Raw)> Probe(
        string headers,
        Encoding? headerEncoding = null)
    {
        var request = $"""
            GET /pi/echo.probe HTTP/1.1
            {headers}
            Connection: close


            """.ReplaceLineEndings("\r\n");
        var raw = await RawSocketProbe.SendRawAsync(
            scenario.Address, (headerEncoding ?? Encoding.ASCII).GetBytes(request));
        var text = Encoding.UTF8.GetString(raw);
        text.Substring(9, 3).ShouldBe("200", text);
        var body = text.Substring(text.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4);
        return (body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1]), raw);
    }

    [Fact]
    public async Task The_Host_Header_Names_The_Server_And_Builds_The_Url()
    {
        var (lines, _) = await Probe("Host: shop.example.com");

        lines["url"].ShouldBe("http://shop.example.com/pi/echo.probe");
        lines["servername"].ShouldBe("shop.example.com");
        lines["serverport"].ShouldBe("80");
        lines["secure"].ShouldBe("false");
        lines["https"].ShouldBe("off");
        lines["applmd"].ShouldBe("/LM/W3SVC/1/ROOT");
        lines["software"].ShouldBe("Kestrel");
    }

    // The test client is a loopback proxy as far as the forwarded-headers trust default goes.
    [Fact]
    public async Task Forwarded_Headers_From_A_Trusted_Proxy_Restore_Scheme_Host_And_Client()
    {
        var (lines, _) = await Probe("""
            Host: 127.0.0.1
            X-Forwarded-Proto: https
            X-Forwarded-Host: front.example.com
            X-Forwarded-For: 203.0.113.9
            """);

        lines["url"].ShouldBe("https://front.example.com/pi/echo.probe");
        lines["secure"].ShouldBe("true");
        lines["https"].ShouldBe("on");
        lines["servername"].ShouldBe("front.example.com");
        lines["serverport"].ShouldBe("443");
        lines["remoteaddr"].ShouldBe("203.0.113.9");
    }

    [Fact]
    public async Task A_Latin1_Request_Header_Byte_Decodes_As_Latin1()
    {
        var (lines, _) = await Probe("Host: 127.0.0.1\nX-Probe: café", Encoding.Latin1);

        lines["xprobe"].ShouldBe("99,97,102,233");
    }

    [Fact]
    public async Task Invalid_Utf8_In_A_Request_Header_Is_Accepted_As_Latin1()
    {
        var (lines, _) = await Probe("Host: 127.0.0.1\nX-Probe: a\u00ff\u00feb", Encoding.Latin1);

        lines["xprobe"].ShouldBe("97,255,254,98");
    }

    [Fact]
    public async Task A_Utf8_Request_Header_Decodes_As_Utf8()
    {
        var (lines, _) = await Probe("Host: 127.0.0.1\nX-Probe: café 中", Encoding.UTF8);

        lines["xprobe"].ShouldBe("99,97,102,233,32,20013");
    }

    [Fact]
    public async Task A_Response_Header_Leaves_In_Utf8_By_Default()
    {
        var (_, raw) = await Probe("Host: 127.0.0.1");

        var headerEnd = raw.AsSpan().IndexOf("\r\n\r\n"u8) + 2;
        raw.AsSpan(0, headerEnd).IndexOf("X-Accent: café\r\n"u8).ShouldBeGreaterThan(0);
    }
}
