using System.Web;

namespace Microsoft.AspNet.FriendlyUrls;

internal sealed class AuthorizationFailureHandler : IHttpHandler
{
    public bool IsReusable => true;

    public void ProcessRequest(HttpContext context)
    {
        context.Response.StatusCode = 401;
    }
}
