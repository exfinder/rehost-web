using System;
using System.Security.Principal;
using Microsoft.AspNet.FriendlyUrls.Resolvers;
using System.Web;
using System.Web.Compilation;
using System.Web.Routing;
using System.Web.Security;

namespace Microsoft.AspNet.FriendlyUrls;

internal sealed class FriendlyUrlRouteHandler : IRouteHandler
{
    private readonly string _fileVirtualPath;
    private readonly IFriendlyUrlResolver _resolver;

    internal FriendlyUrlRouteHandler(
        string fileVirtualPath,
        IFriendlyUrlResolver resolver)
    {
        _fileVirtualPath = fileVirtualPath;
        _resolver = resolver;
    }

    public IHttpHandler GetHttpHandler(RequestContext requestContext)
    {
        ArgumentNullException.ThrowIfNull(requestContext);

        var user = requestContext.HttpContext.User ??
            new GenericPrincipal(new GenericIdentity(string.Empty), []);
        if (!UrlAuthorizationModule.CheckUrlAccessForPrincipal(
                _fileVirtualPath,
                user,
                requestContext.HttpContext.Request.HttpMethod))
        {
            return new AuthorizationFailureHandler();
        }

        var handler = (IHttpHandler)BuildManager.CreateInstanceFromVirtualPath(
            _fileVirtualPath,
            typeof(IHttpHandler));
        _resolver.PreprocessRequest(requestContext.HttpContext, handler);
        return handler;
    }
}
