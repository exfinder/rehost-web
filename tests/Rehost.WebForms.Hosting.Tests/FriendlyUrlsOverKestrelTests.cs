using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class FriendlyUrlsOverKestrelTests(FriendlyUrlsLiveScenario scenario)
    : IClassFixture<FriendlyUrlsLiveScenario>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) " +
        "AppleWebKit/605.1.15 Mobile/15E148 Safari/604.1";

    [Fact]
    public async Task RootServesTheDefaultDocumentThroughTheFriendlyRoute()
    {
        var response = await scenario.Client.GetAsync("/");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldBe("~/default.aspx|/|/default.aspx");
    }

    [Fact]
    public async Task ExtensionlessUrlExecutesPageWithRemainingSegments()
    {
        var response = await scenario.Client.GetAsync("/About/one/two");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldBe("~/About.aspx|one|two");
    }

    [Fact]
    public async Task ExtensionlessUrlPreservesPhysicalPageAuthorization()
    {
        var direct = await scenario.Client.GetAsync("/Protected.aspx");
        var friendly = await scenario.Client.GetAsync("/Protected");

        direct.StatusCode.ShouldBe(401, direct.Text);
        friendly.StatusCode.ShouldBe(401, friendly.Text);
    }

    [Fact]
    public async Task PhysicalPageRedirectsPermanentlyAndPreservesQuery()
    {
        var response = await scenario.Client.GetAsync("/About.aspx?value=one%20two");

        response.StatusCode.ShouldBe(301, response.Text);
        response.Headers["Location"].ShouldBe("/About?value=one%20two");
    }

    [Fact]
    public async Task HelpersResolvePhysicalPathsAndAppendSegments()
    {
        var response = await scenario.Client.GetAsync("/Helpers/current");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldBe("/About|/About/one/3|current");
    }

    [Fact]
    public async Task ExtensionlessUrlExecutesGenericHandler()
    {
        var response = await scenario.Client.GetAsync("/Echo/one");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldBe("~/Echo.ashx|one");
    }

    [Fact]
    public async Task MobileBrowserPrefersMobilePageAndMaster()
    {
        var response = await scenario.Client.GetWithHeadersAsync(
            "/Variant",
            ("User-Agent", MobileUserAgent));

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldContain("mobile-master|mobile-page");
    }

    [Fact]
    public async Task MobileBrowserUsesDesktopPageWhenNoMobilePageExists()
    {
        var response = await scenario.Client.GetWithHeadersAsync(
            "/Fallback",
            ("User-Agent", MobileUserAgent));

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldContain("mobile-master|fallback-page");
    }

    [Fact]
    public async Task SwitchRouteSetsMobileOverrideAndReturnsLocally()
    {
        var switched = await scenario.Client.GetAsync(
            "/__FriendlyUrls_SwitchView/Mobile?ReturnUrl=%2FVariant");

        switched.StatusCode.ShouldBe(302, switched.Text);
        switched.Headers["Location"].ShouldBe("/Variant");
        switched.SetCookies.ShouldHaveSingleItem();
        switched.SetCookies[0].ShouldStartWith(
            "FriendlyUrlsViewSwitcher=Mobile; path=/");

        var cookie = switched.SetCookies[0].Split(';')[0];
        var response = await scenario.Client.GetWithCookiesAsync("/Variant", cookie);
        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldContain("mobile-master|mobile-page");
    }

    [Fact]
    public async Task SwitchRouteDoesNotRedirectOutsideApplication()
    {
        var response = await scenario.Client.GetAsync(
            "/__FriendlyUrls_SwitchView/Desktop" +
            "?ReturnUrl=https%3A%2F%2Fexample.com%2F");

        response.StatusCode.ShouldBe(302, response.Text);
        response.Headers["Location"].ShouldBe("/");
    }
}
