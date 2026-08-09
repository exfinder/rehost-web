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

        var switchRoute = routes[0].ShouldBeOfType<Route>();
        switchRoute.Url.ShouldBe(settings.SwitchViewUrl);
        switchRoute.Defaults["view"].ShouldBe("Desktop");
    }
}
