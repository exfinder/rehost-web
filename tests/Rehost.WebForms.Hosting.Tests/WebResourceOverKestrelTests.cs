using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.UI;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class WebResourceOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    private async Task<string> RenderedUrlAsync()
    {
        var page = await scenario.Client.GetAsync("/WebResourceUrl.aspx");

        page.StatusCode.ShouldBe(200);
        return Regex.Match(page.Text, @"<span id=""url"">([^<]+)</span>").Groups[1].Value;
    }

    [Fact]
    public async Task An_Embedded_Resource_Round_Trips_Through_The_Handler()
    {
        var url = await RenderedUrlAsync();
        url.ShouldStartWith("/WebResource.axd?d=");

        var response = await scenario.Client.GetAsync(url);

        response.StatusCode.ShouldBe(200);
        response.ContentType.ShouldBe("image/gif");

        using var embedded = typeof(Page).Assembly.GetManifestResourceStream("Spacer.gif")!;
        var expected = new byte[embedded.Length];
        embedded.ReadExactly(expected);
        response.Bytes.ShouldBe(expected);
    }

    [Fact]
    public async Task A_Tampered_Payload_Serves_Nothing()
    {
        var url = await RenderedUrlAsync();

        // Only d is protected; t is a cache token the handler does not validate.
        var payload = Regex.Match(url, @"[?&]d=([^&]+)").Groups[1];
        var flipped = payload.Value[0] == 'A' ? 'B' : 'A';
        var tampered = url[..payload.Index] + flipped + url[(payload.Index + 1)..];

        var response = await scenario.Client.GetAsync(tampered);

        response.StatusCode.ShouldBe(404);
    }
}
