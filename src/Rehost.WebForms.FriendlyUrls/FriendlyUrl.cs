using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Routing;

namespace Microsoft.AspNet.FriendlyUrls;

public static class FriendlyUrl
{
    public static IList<string> Segments =>
        HttpContext.Current.Request.GetFriendlyUrlSegments();

    public static string Href(string virtualPath, params object[] segments)
    {
        ValidateVirtualPath(virtualPath);

        var url = GenerateClientUrl(virtualPath);
        if (segments == null || segments.Length == 0)
        {
            return url;
        }

        var builder = new StringBuilder(url);
        foreach (var segment in segments)
        {
            var encoded = HttpUtility.UrlEncode(
                Convert.ToString(segment, CultureInfo.InvariantCulture));
            if (string.IsNullOrEmpty(encoded))
            {
                continue;
            }

            encoded = encoded.Replace("+", "%20");
            if (builder[^1] != '/')
            {
                builder.Append('/');
            }

            builder.Append(encoded);
        }

        return builder.ToString();
    }

    public static string Resolve(string virtualPath)
    {
        return RouteTable.Routes["AspNet.FriendlyUrls"] is FriendlyUrlRoute route
            ? route.ResolveUrl(virtualPath)
            : VirtualPathUtility.ToAbsolute(virtualPath);
    }

    private static string GenerateClientUrl(string virtualPath)
    {
        var suffixStart = virtualPath.IndexOfAny(['?', '#']);
        var path = suffixStart >= 0 ? virtualPath[..suffixStart] : virtualPath;
        var suffix = suffixStart >= 0 ? virtualPath[suffixStart..] : string.Empty;
        if (path.Length > 0 && path[0] == '~')
        {
            path = VirtualPathUtility.ToAbsolute(path);
        }

        return path + suffix;
    }

    private static void ValidateVirtualPath(string virtualPath)
    {
        if (virtualPath != null && virtualPath.Length > 0 &&
            ((virtualPath[0] == '~' && virtualPath.Length > 1 && virtualPath[1] == '/') ||
             (virtualPath[0] == '/' &&
              (virtualPath.Length == 1 ||
               (virtualPath[1] != '/' && virtualPath[1] != '\\')))))
        {
            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(virtualPath),
            "The virtual path must be app-relative ('~/') or absolute ('/').");
    }
}
