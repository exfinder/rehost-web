using System.Web;

namespace Rehost.Web.ScenarioProbes;

public sealed class CookieHandler : IHttpHandler
{
    // Fixed and UTC so the rendered expires attribute does not depend on the host's clock or time
    // zone: FormatHttpCookieDateTime converts to universal time before formatting.
    private static readonly DateTime Expiry = new(2035, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var request = context.Request;
        var received = Describe(request.Cookies);

        switch (request.QueryString["mode"])
        {
            case "pair":
                context.Response.Cookies.Add(new HttpCookie("first", "1"));
                context.Response.Cookies.Add(new HttpCookie("second", "2"));
                break;
            case "unicode":
                context.Response.Cookies.Add(new HttpCookie("place", "caf\u00e9"));
                break;
            case "attributes":
                context.Response.Cookies.Add(new HttpCookie("marked", "value")
                {
                    Path = "/scoped",
                    Domain = "example.test",
                    Expires = Expiry,
                    Secure = true,
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                });
                break;
            case "reissue":
                var carried = request.Cookies["a"];
                if (carried != null)
                {
                    context.Response.Cookies.Add(carried);
                }

                break;
        }

        RequestBodyHandler.WriteResult(context, received);
    }

    private static string Describe(HttpCookieCollection cookies)
    {
        var parts = new List<string>();

        for (var i = 0; i < cookies.Count; i++)
        {
            var cookie = cookies[i];
            var part = cookies.GetKey(i) + "=" + cookie.Value;

            if (cookie.HasKeys)
            {
                var subkeys = new List<string>();
                foreach (string? key in cookie.Values)
                {
                    subkeys.Add(key + ":" + cookie.Values[key]);
                }

                part += "{" + string.Join(",", subkeys) + "}";
            }

            parts.Add(part);
        }

        return parts.Count == 0 ? "none" : string.Join("|", parts);
    }
}
