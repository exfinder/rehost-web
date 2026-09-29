using System.Web.Routing;

namespace Microsoft.AspNet.FriendlyUrls;

// Link generation reaches the switcher route only through the magic key, so ordinary
// RouteUrl lookups never accidentally produce a view-switch URL.
internal sealed class FriendlyUrlsViewSwitcherRoute : Route
{
    private const string ViewSwitcherMagicKey = "__FriendlyUrls_SwitchViews";

    internal FriendlyUrlsViewSwitcherRoute(
        string url,
        RouteValueDictionary defaults,
        IRouteHandler routeHandler)
        : base(url, defaults, routeHandler)
    {
    }

    public override VirtualPathData? GetVirtualPath(
        RequestContext requestContext,
        RouteValueDictionary values)
    {
        if (values != null && values.ContainsKey(ViewSwitcherMagicKey))
        {
            var trimmed = new RouteValueDictionary(values);
            trimmed.Remove(ViewSwitcherMagicKey);
            return base.GetVirtualPath(requestContext, trimmed);
        }

        return null;
    }
}
