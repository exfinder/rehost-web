using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Routing;

namespace Microsoft.AspNet.FriendlyUrls;

public static class HttpRequestExtensions
{
    private static readonly IList<string> EmptySegments = new List<string>().AsReadOnly();

    public static IList<string> GetFriendlyUrlSegments(this HttpRequest httpRequest)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        return new HttpRequestWrapper(httpRequest).GetFriendlyUrlSegments();
    }

    public static IList<string> GetFriendlyUrlSegments(this HttpRequestBase httpRequest)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        return httpRequest.GetRouteData().DataTokens["FriendlyUrlSegments"] as IList<string>
            ?? EmptySegments;
    }

    public static string GetFriendlyUrlFileExtension(this HttpRequest httpRequest)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        return new HttpRequestWrapper(httpRequest).GetFriendlyUrlFileExtension();
    }

    public static string GetFriendlyUrlFileExtension(this HttpRequestBase httpRequest)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        return httpRequest.GetRouteData().DataTokens["FriendlyUrlFileExtension"] as string
            ?? string.Empty;
    }

    public static string GetFriendlyUrlFileVirtualPath(this HttpRequest httpRequest)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        return new HttpRequestWrapper(httpRequest).GetFriendlyUrlFileVirtualPath();
    }

    public static string GetFriendlyUrlFileVirtualPath(this HttpRequestBase httpRequest)
    {
        ArgumentNullException.ThrowIfNull(httpRequest);
        return httpRequest.GetRouteData().DataTokens["FriendlyUrlFileVirtualPath"] as string
            ?? string.Empty;
    }

    // A request served through the module's rewritten-URL remap has its friendly route
    // data stashed in Items rather than in RequestContext.RouteData.
    internal static RouteData GetRouteData(this HttpRequestBase httpRequest)
    {
        return httpRequest.RequestContext.HttpContext.Items[FriendlyUrlsModule.RouteDataItemsKey]
            as RouteData
            ?? httpRequest.RequestContext.RouteData;
    }
}
