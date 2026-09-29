using System.Collections;
using Microsoft.AspNet.FriendlyUrls.Resolvers;
using Shouldly;
using System.Web;
using Xunit;

namespace Rehost.AspNet.FriendlyUrls.Tests.Resolvers;

public sealed class WebFormsFriendlyUrlResolverTests
{
    [Fact]
    public void MobileBrowserPrefersMobilePageThenDesktopFallback()
    {
        var context = new TestHttpContext(browserIsMobile: true);
        var resolver = new WebFormsFriendlyUrlResolver();

        resolver.GetExtensions(context).ShouldBe([".Mobile.aspx", ".aspx"]);
    }

    [Fact]
    public void DesktopViewOverrideBeatsTheMobileBrowser()
    {
        var context = new TestHttpContext(browserIsMobile: true);
        context.Items[WebFormsFriendlyUrlResolver.ViewSwitcherCookieName] = "Desktop";
        var resolver = new WebFormsFriendlyUrlResolver();

        resolver.GetExtensions(context).ShouldBe([".aspx"]);
    }

    [Fact]
    public void MobileViewOverrideBeatsTheDesktopBrowser()
    {
        var context = new TestHttpContext(browserIsMobile: false);
        context.Items[WebFormsFriendlyUrlResolver.ViewSwitcherCookieName] = "Mobile";
        var resolver = new WebFormsFriendlyUrlResolver();

        resolver.GetExtensions(context).ShouldBe([".Mobile.aspx", ".aspx"]);
    }

    [Fact]
    public void IsMobileViewReflectsTheResolvedViewFlagNotTheDevice()
    {
        var context = new TestHttpContext(browserIsMobile: true);

        WebFormsFriendlyUrlResolver.IsMobileView(context).ShouldBeFalse();

        context.Items["AspNet.FriendlyUrls.IsMobile"] = true;

        WebFormsFriendlyUrlResolver.IsMobileView(context).ShouldBeTrue();
    }

    private sealed class TestHttpContext : HttpContextBase
    {
        private readonly HttpRequestBase _request;
        private readonly Hashtable _items = [];

        internal TestHttpContext(bool browserIsMobile)
        {
            _request = new TestHttpRequest(browserIsMobile);
        }

        public override HttpRequestBase Request => _request;

        public override IDictionary Items => _items;
    }

    private sealed class TestHttpRequest : HttpRequestBase
    {
        private readonly HttpBrowserCapabilitiesBase _browser;
        private readonly HttpCookieCollection _cookies = new();

        internal TestHttpRequest(bool browserIsMobile)
        {
            _browser = new TestBrowserCapabilities(browserIsMobile);
        }

        public override HttpBrowserCapabilitiesBase Browser => _browser;

        public override HttpCookieCollection Cookies => _cookies;
    }

    private sealed class TestBrowserCapabilities(bool isMobile) : HttpBrowserCapabilitiesBase
    {
        public override bool IsMobileDevice => isMobile;
    }
}
