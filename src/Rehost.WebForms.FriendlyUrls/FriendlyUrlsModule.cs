using System;
using System.Web;
using System.Web.Routing;

namespace Microsoft.AspNet.FriendlyUrls;

internal sealed class FriendlyUrlsModule : IHttpModule
{
    internal static readonly object RouteDataItemsKey = new();

    public void Init(HttpApplication context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.PostMapRequestHandler += OnPostMapRequestHandler;
    }

    public void Dispose()
    {
    }

    private static void OnPostMapRequestHandler(object? sender, EventArgs e)
    {
        var application = (HttpApplication)sender!;
        var context = application.Context;
        if (RouteTable.Routes["AspNet.FriendlyUrls"] is not FriendlyUrlRoute route)
        {
            return;
        }

        var request = context.Request;
        var currentExecutionFilePath = request.CurrentExecutionFilePath;
        if (string.IsNullOrEmpty(VirtualPathUtility.GetExtension(currentExecutionFilePath)))
        {
            return;
        }

        var friendlyUrl = route.ResolveUrl(currentExecutionFilePath);
        if (string.Equals(friendlyUrl, currentExecutionFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var target = friendlyUrl + request.PathInfo + request.Url.Query;

        // The execution path carries an extension the client never typed exactly when
        // something rewrote the URL (IIS default document, URL rewriting): serve the
        // rewritten request through the friendly route instead of redirecting, so the
        // client-visible URL survives and the page still sees friendly route data.
        if (request.RawUrl.Contains(currentExecutionFilePath, StringComparison.OrdinalIgnoreCase))
        {
            if (route.Settings.AutoRedirectMode == RedirectMode.Temporary)
            {
                context.Response.Redirect(target);
            }
            else if (route.Settings.AutoRedirectMode == RedirectMode.Permanent)
            {
                context.Response.RedirectPermanent(target);
            }
        }
        else
        {
            var routeData = route.GetRouteData(
                new HttpContextWrapper(context),
                VirtualPathUtility.ToAppRelative(friendlyUrl));
            context.Items[RouteDataItemsKey] = routeData;
            context.Handler = routeData!.RouteHandler.GetHttpHandler(request.RequestContext);
        }
    }
}
