using System;
using System.Web;
using System.Web.Routing;
using Microsoft.AspNet.FriendlyUrls.Resolvers;

namespace Microsoft.AspNet.FriendlyUrls;

internal sealed class SwitchViewRouteHandler : IRouteHandler
{
    public IHttpHandler GetHttpHandler(RequestContext requestContext)
    {
        return new SwitchViewHandler();
    }

    private sealed class SwitchViewHandler : IHttpHandler
    {
        public bool IsReusable => false;

        public void ProcessRequest(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var view = (string?)context.Request.RequestContext.RouteData.Values["view"];
            if (view == null)
            {
                return;
            }

            if (!IsValidViewName(view))
            {
                throw new HttpException(400, "Bad request.");
            }

            var cookie = new HttpCookie(WebFormsFriendlyUrlResolver.ViewSwitcherCookieName, view);
            if (context.Request.IsSecureConnection)
            {
                cookie.Secure = true;
            }

            context.Response.Cookies.Add(cookie);
            var returnUrl = context.Request.QueryString["ReturnUrl"] ?? "~/";
            SafeRedirect(context.Response, returnUrl);
        }

        private static void SafeRedirect(HttpResponse response, string url)
        {
            if (IsLocalUrl(url))
            {
                response.Redirect(url, true);
            }
            else
            {
                response.Redirect("~/");
            }
        }

        private static bool IsLocalUrl(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            if (url[0] == '/')
            {
                return url.Length == 1 || (url[1] != '/' && url[1] != '\\');
            }

            return url.Length > 1 && url[0] == '~' && url[1] == '/';
        }

        private static bool IsValidViewName(string value)
        {
            // The shipped binary's per-character guard is a dead condition (an &&-chain no
            // character can satisfy), so it accepts every view name; mirrored deliberately.
            foreach (var c in value)
            {
                if (c < '0' && c > '9' && c < 'a' && c > 'z' && c < 'A' && c > 'Z' &&
                    c != '.' && c != '-' && c != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
