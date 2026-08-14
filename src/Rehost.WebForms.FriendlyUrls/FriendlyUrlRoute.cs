using System;
using System.Collections.Generic;
using Microsoft.AspNet.FriendlyUrls.Resolvers;
using System.Security.Principal;
using System.Web;
using System.Web.Compilation;
using System.Web.Hosting;
using System.Web.Routing;
using System.Web.Security;
using System.Web.Util;

namespace Microsoft.AspNet.FriendlyUrls;

internal sealed class FriendlyUrlRoute : RouteBase
{
    // Upstream passes a one-null-entry array when a resolver returns no extension list,
    // which makes the walk probe the bare path with no extension appended.
    private static readonly string?[] NoExtensionFallback = new string?[1];

    private static readonly IPrincipal DummyPrincipal =
        new GenericPrincipal(new GenericIdentity(string.Empty), []);

    private readonly FriendlyUrlSettings _settings;
    private readonly IFriendlyUrlResolver[] _resolvers;
    private readonly Lazy<FriendlyUrlFileCache?> _fileCache;

    internal FriendlyUrlRoute(
        FriendlyUrlSettings settings,
        IFriendlyUrlResolver[] resolvers)
    {
        _settings = settings;
        _resolvers = resolvers;
        _fileCache = FriendlyUrlFileCache.CreateLazy(settings.ResolverCachingMode);
    }

    internal FriendlyUrlSettings Settings => _settings;

    internal string ResolveUrl(string virtualPath)
    {
        var path = VirtualPathUtility.IsAppRelative(virtualPath)
            ? VirtualPathUtility.ToAbsolute(virtualPath)
            : virtualPath;
        if (string.IsNullOrEmpty(VirtualPathUtility.GetExtension(path)))
        {
            return path;
        }

        foreach (var resolver in _resolvers)
        {
            var friendlyUrl = resolver.ConvertToFriendlyUrl(path);
            if (friendlyUrl != null)
            {
                return friendlyUrl;
            }
        }

        return path;
    }

    public override RouteData? GetRouteData(HttpContextBase httpContext)
    {
        return GetRouteData(httpContext, pathOverride: null);
    }

    internal RouteData? GetRouteData(HttpContextBase httpContext, string? pathOverride)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ResolveResult? result = null;
        IFriendlyUrlResolver? matchedResolver = null;
        foreach (var resolver in _resolvers)
        {
            var extensions = (IList<string?>?)resolver.GetExtensions(httpContext)
                ?? NoExtensionFallback;
            try
            {
                result = Resolve(httpContext, extensions, pathOverride);
            }
            catch (HttpException exception) when (exception.GetHttpCode() == 401)
            {
                MarkUnauthorizedAndCompleteRequest(httpContext);
                return new RouteData(this, new AuthorizationFailedRouteHandler());
            }

            if (result != null)
            {
                matchedResolver = resolver;
                break;
            }
        }

        if (result == null)
        {
            return null;
        }

        var routeData = new RouteData(
            this,
            new FriendlyUrlRouteHandler(result.WebObjectFactory!, matchedResolver!));
        routeData.DataTokens["FriendlyUrlFileExtension"] = result.ResolvedExtension;
        routeData.DataTokens["FriendlyUrlFileVirtualPath"] = result.ResolvedVirtualPath;
        routeData.DataTokens["FriendlyUrlSegments"] = result.Segments;
        return routeData;
    }

    public override VirtualPathData? GetVirtualPath(
        RequestContext requestContext,
        RouteValueDictionary values)
    {
        return null;
    }

    private ResolveResult? Resolve(
        HttpContextBase httpContext,
        IList<string?> extensions,
        string? pathOverride)
    {
        var result = new ResolveResult();
        var leftoverSegments = new Stack<string>();
        var path = pathOverride ?? httpContext.Request.AppRelativeCurrentExecutionFilePath!;
        while (path.Length > 2)
        {
            var separator = path.LastIndexOf('/');
            if (separator != path.Length - 1)
            {
                foreach (var extension in extensions)
                {
                    var candidate = path + extension;
                    var factory = GetWebObjectFactory(httpContext, candidate);
                    if (factory == null)
                    {
                        continue;
                    }

                    foreach (var segment in leftoverSegments)
                    {
                        result.Segments.Add(segment);
                    }

                    result.ResolvedExtension = extension;
                    result.ResolvedVirtualPath = candidate;
                    result.WebObjectFactory = factory;
                    return result;
                }

                leftoverSegments.Push(path[(separator + 1)..]);
            }

            path = path[..separator];
        }

        return null;
    }

    private IWebObjectFactory? GetWebObjectFactory(
        HttpContextBase httpContext,
        string virtualPath)
    {
        var fileCache = _fileCache.Value;
        if (fileCache == null || !fileCache.FileExists(virtualPath))
        {
            return null;
        }

        var factory = BuildManager.GetObjectFactory(virtualPath, throwIfNotFound: false);
        if (factory != null &&
            !httpContext.SkipAuthorization &&
            !UrlAuthorizationModule.CheckUrlAccessForPrincipal(
                virtualPath,
                httpContext.User ?? DummyPrincipal,
                httpContext.Request.HttpMethod))
        {
            throw new HttpException(401, "Access is denied.");
        }

        return factory;
    }

    private static void MarkUnauthorizedAndCompleteRequest(HttpContextBase httpContext)
    {
        httpContext.Response.StatusCode = 401;
        var application = httpContext.ApplicationInstance;
        if (application != null)
        {
            application.CompleteRequest();
        }
        else
        {
            httpContext.Response.End();
        }
    }

    private sealed class ResolveResult
    {
        internal IWebObjectFactory? WebObjectFactory { get; set; }

        internal string? ResolvedExtension { get; set; }

        internal string? ResolvedVirtualPath { get; set; }

        internal IList<string> Segments { get; } = new List<string>();
    }

    private sealed class FriendlyUrlRouteHandler : IRouteHandler
    {
        private readonly IWebObjectFactory _factory;
        private readonly IFriendlyUrlResolver _resolver;

        internal FriendlyUrlRouteHandler(
            IWebObjectFactory factory,
            IFriendlyUrlResolver resolver)
        {
            _factory = factory;
            _resolver = resolver;
        }

        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            ArgumentNullException.ThrowIfNull(requestContext);

            var handler = (IHttpHandler)_factory.CreateInstance();
            _resolver.PreprocessRequest(requestContext.HttpContext, handler);
            return handler;
        }
    }

    private sealed class AuthorizationFailedRouteHandler : IRouteHandler
    {
        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            return new AuthorizationFailedHttpHandler();
        }

        private sealed class AuthorizationFailedHttpHandler : IHttpHandler
        {
            public bool IsReusable => false;

            public void ProcessRequest(HttpContext context)
            {
                MarkUnauthorizedAndCompleteRequest(new HttpContextWrapper(context));
            }
        }
    }
}
