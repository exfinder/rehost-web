using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Web.Routing;

namespace Microsoft.AspNet.FriendlyUrls;

public static class FriendlyUrl
{
    public static IList<string> Segments =>
        HttpContext.Current.Request.GetFriendlyUrlSegments();

    public static string Href(string virtualPath, params object[] pathSegments)
    {
        var friendlyUrl = Resolve(virtualPath);
        if (pathSegments == null || pathSegments.Length == 0)
        {
            return friendlyUrl;
        }

        var segments = new string[pathSegments.Length];
        for (var index = 0; index < pathSegments.Length; index++)
        {
            var value = Convert.ToString(pathSegments[index], CultureInfo.InvariantCulture) ?? string.Empty;
            segments[index] = Uri.EscapeDataString(value);
        }

        return friendlyUrl.TrimEnd('/') + "/" + string.Join("/", segments);
    }

    public static string Resolve(string virtualPath)
    {
        ValidateVirtualPath(virtualPath);

        var suffixStart = virtualPath.IndexOfAny(['?', '#']);
        var path = suffixStart >= 0 ? virtualPath[..suffixStart] : virtualPath;
        var suffix = suffixStart >= 0 ? virtualPath[suffixStart..] : string.Empty;
        var route = RouteTable.Routes["AspNet.FriendlyUrls"] as FriendlyUrlRoute;
        var resolved = route?.ConvertToFriendlyUrl(path) ?? VirtualPathUtility.ToAbsolute(path);
        return resolved + suffix;
    }

    private static void ValidateVirtualPath(string virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath) ||
            (!virtualPath.StartsWith("~/", StringComparison.Ordinal) &&
             virtualPath[0] != '/'))
        {
            throw new ArgumentException(
                "The provided virtual path must be app-relative or absolute.",
                nameof(virtualPath));
        }
    }
}
