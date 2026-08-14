using Microsoft.AspNet.FriendlyUrls;
using Shouldly;
using System.Web.Routing;
using Xunit;

namespace Rehost.WebForms.FriendlyUrls.Tests;

public sealed class RouteCollectionExtensionsTests
{
    [Fact]
    public void EnableFriendlyUrlsRegistersFrameworkRouteOrder()
    {
        var routes = new RouteCollection();
        var settings = new FriendlyUrlSettings();

        routes.EnableFriendlyUrls(settings);

        routes.Count.ShouldBe(2);
        routes[settings.SwitchViewRouteName].ShouldBeSameAs(routes[0]);

        var switchRoute = routes[0].ShouldBeAssignableTo<Route>()!;
        switchRoute.Url.ShouldBe(settings.SwitchViewUrl);
        switchRoute.Defaults["view"].ShouldBe("Desktop");
    }

    [Fact]
    public void EnableFriendlyUrlsRejectsAnEmptyResolverList()
    {
        var routes = new RouteCollection();

        Should.Throw<ArgumentException>(() => routes.EnableFriendlyUrls(
            new FriendlyUrlSettings(),
            Array.Empty<Microsoft.AspNet.FriendlyUrls.Resolvers.IFriendlyUrlResolver>()));
    }

    [Fact]
    public void SwitchViewRouteGeneratesUrlsOnlyThroughTheMagicKey()
    {
        var routes = new RouteCollection();
        routes.EnableFriendlyUrls(new FriendlyUrlSettings());
        var switchRoute = (Route)routes[0];

        var requestContext = new RequestContext(
            new StubHttpContext(),
            new RouteData(switchRoute, null));

        switchRoute.GetVirtualPath(
                requestContext,
                new RouteValueDictionary { ["view"] = "Mobile" })
            .ShouldBeNull();
        switchRoute.GetVirtualPath(
                requestContext,
                new RouteValueDictionary
                {
                    ["view"] = "Mobile",
                    ["__FriendlyUrls_SwitchViews"] = true
                })
            .ShouldNotBeNull();
    }

    private sealed class StubHttpContext : System.Web.HttpContextBase
    {
        private readonly System.Web.HttpRequestBase _request = new StubHttpRequest();

        public override System.Web.HttpRequestBase Request => _request;
    }

    private sealed class StubHttpRequest : System.Web.HttpRequestBase
    {
        public override string ApplicationPath => "/";

        public override string AppRelativeCurrentExecutionFilePath => "~/";

        public override string PathInfo => string.Empty;
    }
}
