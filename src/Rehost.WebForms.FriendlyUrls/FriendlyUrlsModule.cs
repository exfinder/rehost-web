using System;
using System.Web;
using System.Web.Routing;

namespace Microsoft.AspNet.FriendlyUrls;

internal sealed class FriendlyUrlsModule : IHttpModule
{
    public void Init(HttpApplication context)
    {
        context.PostMapRequestHandler += OnPostMapRequestHandler;
    }

    public void Dispose()
    {
    }

    private static void OnPostMapRequestHandler(object? sender, EventArgs e)
    {
        var application = (HttpApplication)sender!;
        var context = application.Context;
        if (RouteTable.Routes["AspNet.FriendlyUrls"] is not FriendlyUrlRoute route ||
            route.Settings.AutoRedirectMode == RedirectMode.Off ||
            WasUrlRewritten(context.Request))
        {
            return;
        }

        var requestPath = context.Request.AppRelativeCurrentExecutionFilePath;
        if (string.IsNullOrEmpty(requestPath))
        {
            return;
        }

        var friendlyUrl = route.ConvertToFriendlyUrl(requestPath);
        if (friendlyUrl == null)
        {
            return;
        }

        var queryStart = context.Request.RawUrl.IndexOf('?', StringComparison.Ordinal);
        if (queryStart >= 0)
        {
            friendlyUrl += context.Request.RawUrl[queryStart..];
        }

        if (route.Settings.AutoRedirectMode == RedirectMode.Permanent)
        {
            context.Response.RedirectPermanent(friendlyUrl, false);
        }
        else
        {
            context.Response.Redirect(friendlyUrl, false);
        }
    }

    private static bool WasUrlRewritten(HttpRequest request)
    {
        return !string.IsNullOrEmpty(request.ServerVariables["IIS_UrlRewriteModule"]) ||
            string.Equals(
                request.ServerVariables["IIS_WasUrlRewritten"],
                "1",
                StringComparison.OrdinalIgnoreCase);
    }
}
