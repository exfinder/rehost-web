using Microsoft.AspNet.FriendlyUrls.Resolvers;
using Shouldly;
using System.Web;
using Xunit;

namespace Rehost.WebForms.FriendlyUrls.Tests.Resolvers;

public sealed class WebFormsFriendlyUrlResolverTests
{
    [Fact]
    public void MobileBrowserPrefersMobilePageThenDesktopFallback()
    {
        var context = new TestHttpContext(browserIsMobile: true);
        var resolver = new WebFormsFriendlyUrlResolver();

        WebFormsFriendlyUrlResolver.IsMobileView(context).ShouldBeTrue();
        resolver.GetExtensions(context).ShouldBe([".Mobile.aspx", ".aspx"]);
    }

    private sealed class TestHttpContext : HttpContextBase
    {
        private readonly HttpRequestBase _request;

        internal TestHttpContext(bool browserIsMobile)
        {
            _request = new TestHttpRequest(browserIsMobile);
        }

        public override HttpRequestBase Request => _request;
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
