using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// <globalization responseHeaderEncoding="iso-8859-1"> puts Latin-1 bytes on the wire (IIS
// reading R4: X-Accent: caf\xE9). The application copy is edited before its first request, so
// the host activates on the amended configuration.
public sealed class ResponseHeaderEncodingOverKestrelTests
{
    [Fact]
    public async Task A_Configured_Latin1_Header_Encoding_Reaches_The_Wire()
    {
        using var scenario = LiveScenario.StartIsolated(
            Fixtures.Page, IsolationReason.HostConfiguration);
        var config = Path.Combine(scenario.ApplicationPath, "web.config");
        File.WriteAllText(config, File.ReadAllText(config).Replace(
            "<system.web>",
            """
            <system.web>
                <globalization responseHeaderEncoding="iso-8859-1" />
            """));

        var raw = await RawSocketProbe.GetRawResponseAsync(scenario.Address, "/pi/echo.probe");

        var text = Encoding.ASCII.GetString(raw);
        text.Substring(9, 3).ShouldBe("200", text);
        var headers = raw.AsSpan(0, raw.AsSpan().IndexOf("\r\n\r\n"u8) + 2);
        byte[] expected = [.. "X-Accent: caf"u8, 0xE9, (byte)'\r', (byte)'\n'];
        headers.IndexOf(expected).ShouldBeGreaterThan(0);
    }
}
