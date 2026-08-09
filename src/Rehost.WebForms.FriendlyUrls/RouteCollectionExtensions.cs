using System;
using Microsoft.AspNet.FriendlyUrls.Resolvers;
using System.Web.Routing;

namespace Microsoft.AspNet.FriendlyUrls;

public static class RouteCollectionExtensions
{
    public static void EnableFriendlyUrls(this RouteCollection routes)
    {
        EnableFriendlyUrls(routes, new FriendlyUrlSettings());
    }

    public static void EnableFriendlyUrls(
        this RouteCollection routes,
        FriendlyUrlSettings settings)
    {
        EnableFriendlyUrls(routes, settings, CreateDefaultResolvers());
    }

    public static void EnableFriendlyUrls(
        this RouteCollection routes,
        params IFriendlyUrlResolver[] resolvers)
    {
        EnableFriendlyUrls(routes, new FriendlyUrlSettings(), resolvers);
    }

    public static void EnableFriendlyUrls(
        this RouteCollection routes,
        FriendlyUrlSettings settings,
        params IFriendlyUrlResolver[] resolvers)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(resolvers);

        var switchRoute = new Route(
            settings.SwitchViewUrl,
            new RouteValueDictionary { ["view"] = "Desktop" },
            new SwitchViewRouteHandler());

        routes.Add(settings.SwitchViewRouteName, switchRoute);
        routes.Add("AspNet.FriendlyUrls", new FriendlyUrlRoute(settings, resolvers));
    }

    private static IFriendlyUrlResolver[] CreateDefaultResolvers()
    {
        return [
            new WebFormsFriendlyUrlResolver(),
            new GenericHandlerFriendlyUrlResolver()
        ];
    }
}
