using System.Collections.Generic;
using System.Web;

namespace Microsoft.AspNet.FriendlyUrls;

public static class HttpRequestExtensions
{
    private static readonly IList<string> EmptySegments = new List<string>().AsReadOnly();

    public static IList<string> GetFriendlyUrlSegments(this HttpRequest httpRequest)
    {
        return GetFriendlyUrlSegments(new HttpRequestWrapper(httpRequest));
    }

    public static IList<string> GetFriendlyUrlSegments(this HttpRequestBase httpRequest)
    {
        return GetDataToken<IList<string>>(httpRequest, "FriendlyUrlSegments") ?? EmptySegments;
    }

    public static string? GetFriendlyUrlFileExtension(this HttpRequest httpRequest)
    {
        return GetFriendlyUrlFileExtension(new HttpRequestWrapper(httpRequest));
    }

    public static string? GetFriendlyUrlFileExtension(this HttpRequestBase httpRequest)
    {
        return GetDataToken<string>(httpRequest, "FriendlyUrlFileExtension");
    }

    public static string? GetFriendlyUrlFileVirtualPath(this HttpRequest httpRequest)
    {
        return GetFriendlyUrlFileVirtualPath(new HttpRequestWrapper(httpRequest));
    }

    public static string? GetFriendlyUrlFileVirtualPath(this HttpRequestBase httpRequest)
    {
        return GetDataToken<string>(httpRequest, "FriendlyUrlFileVirtualPath");
    }

    private static T? GetDataToken<T>(HttpRequestBase request, string key)
        where T : class
    {
        return request.RequestContext?.RouteData.DataTokens[key] as T;
    }
}
