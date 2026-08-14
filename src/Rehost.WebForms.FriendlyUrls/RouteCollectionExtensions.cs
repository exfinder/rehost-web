using System;
using Microsoft.AspNet.FriendlyUrls.Resolvers;
using System.Web.Routing;

namespace Microsoft.AspNet.FriendlyUrls;

public static class RouteCollectionExtensions
{
    public static void EnableFriendlyUrls(this RouteCollection routes)
    {
        EnableFriendlyUrls(routes, new WebFormsFriendlyUrlResolver());
    }

    public static void EnableFriendlyUrls(
        this RouteCollection routes,
        FriendlyUrlSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        EnableFriendlyUrls(
            routes,
            settings,
            new WebFormsFriendlyUrlResolver(settings.ResolverCachingMode));
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
        if (resolvers.Length == 0)
        {
            throw new ArgumentException(
                "The enumerable argument must contain at least one item.",
                nameof(resolvers));
        }

        routes.Add(
            settings.SwitchViewRouteName,
            new FriendlyUrlsViewSwitcherRoute(
                settings.SwitchViewUrl,
                new RouteValueDictionary { ["view"] = "Desktop" },
                new SwitchViewRouteHandler()));
        routes.Add("AspNet.FriendlyUrls", new FriendlyUrlRoute(settings, resolvers));
    }
}
