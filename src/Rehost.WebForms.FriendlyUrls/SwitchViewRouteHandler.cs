using System;
using System.Web;
using System.Web.Routing;
using Microsoft.AspNet.FriendlyUrls.Resolvers;

namespace Microsoft.AspNet.FriendlyUrls;

internal sealed class SwitchViewRouteHandler : IRouteHandler
{
    public IHttpHandler GetHttpHandler(RequestContext requestContext)
    {
        ArgumentNullException.ThrowIfNull(requestContext);

        var view = Convert.ToString(
            requestContext.RouteData.Values["view"],
            System.Globalization.CultureInfo.InvariantCulture);
        return new SwitchViewHandler(
            string.Equals(view, "Mobile", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class SwitchViewHandler(bool mobileView) : IHttpHandler
    {
        public bool IsReusable => false;

        public void ProcessRequest(HttpContext context)
        {
            context.Response.Cookies.Add(new HttpCookie(
                WebFormsFriendlyUrlResolver.ViewSwitcherCookieName,
                mobileView.ToString()));

            var returnUrl = context.Request.QueryString["ReturnUrl"];
            if (!IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            context.Response.Redirect(returnUrl, false);
        }

        private static bool IsLocalUrl(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            if (url[0] == '/')
            {
                return url.Length == 1 ||
                    (url[1] != '/' && url[1] != '\\');
            }

            return url.Length > 1 && url[0] == '~' && url[1] == '/';
        }
    }
}
