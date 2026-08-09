using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.FriendlyUrls.Resolvers;
using System.Web;
using System.Web.Hosting;
using System.Web.Routing;

namespace Microsoft.AspNet.FriendlyUrls;

internal sealed class FriendlyUrlRoute : RouteBase
{
    private readonly FriendlyUrlSettings _settings;
    private readonly IFriendlyUrlResolver[] _resolvers;
    private readonly Lazy<FriendlyUrlFileCache?> _fileCache;

    internal FriendlyUrlRoute(
        FriendlyUrlSettings settings,
        IFriendlyUrlResolver[] resolvers)
    {
        _settings = settings;
        _resolvers = resolvers;
        _fileCache = new Lazy<FriendlyUrlFileCache?>(() =>
        {
            var provider = HostingEnvironment.VirtualPathProvider;
            if (provider == null)
            {
                return null;
            }

            var applicationVirtualPath = HostingEnvironment.ApplicationVirtualPath;
            if (string.IsNullOrEmpty(applicationVirtualPath))
            {
                applicationVirtualPath = "~/";
            }

            return new FriendlyUrlFileCache(
                settings.ResolverCachingMode,
                provider,
                applicationVirtualPath,
                HttpRuntimeCacheStore.Instance);
        });
    }

    internal FriendlyUrlSettings Settings => _settings;

    internal string? ConvertToFriendlyUrl(string path)
    {
        foreach (var resolver in _resolvers)
        {
            var friendlyUrl = resolver.ConvertToFriendlyUrl(path);
            if (friendlyUrl != null)
            {
                return friendlyUrl;
            }
        }

        return null;
    }

    public override RouteData? GetRouteData(HttpContextBase httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var requestPath = httpContext.Request.AppRelativeCurrentExecutionFilePath;
        if (string.IsNullOrEmpty(requestPath) || !requestPath.StartsWith("~/", StringComparison.Ordinal))
        {
            return null;
        }

        var path = requestPath[2..] + httpContext.Request.PathInfo;
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var fileCache = _fileCache.Value;
        if (fileCache == null)
        {
            return null;
        }

        for (var fileSegmentCount = pathSegments.Length; fileSegmentCount > 0; fileSegmentCount--)
        {
            var friendlyPath = "~/" + string.Join("/", pathSegments, 0, fileSegmentCount);

            foreach (var resolver in _resolvers)
            {
                foreach (var extension in resolver.GetExtensions(httpContext))
                {
                    var fileVirtualPath = friendlyPath + extension;
                    if (!fileCache.FileExists(fileVirtualPath))
                    {
                        continue;
                    }

                    var remainingSegments = pathSegments.Skip(fileSegmentCount).ToList();
                    var handler = new FriendlyUrlRouteHandler(fileVirtualPath, resolver);
                    var routeData = new RouteData(this, handler);
                    routeData.DataTokens["FriendlyUrlFileExtension"] = extension;
                    routeData.DataTokens["FriendlyUrlFileVirtualPath"] = fileVirtualPath;
                    routeData.DataTokens["FriendlyUrlSegments"] = remainingSegments;
                    return routeData;
                }
            }
        }

        return null;
    }

    public override VirtualPathData? GetVirtualPath(
        RequestContext requestContext,
        RouteValueDictionary values)
    {
        return null;
    }
}
