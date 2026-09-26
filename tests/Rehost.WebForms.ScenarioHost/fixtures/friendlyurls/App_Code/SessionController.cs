using System.Web;
using System.Web.Http;
using System.Web.Http.WebHost;
using System.Web.Routing;
using System.Web.SessionState;

public class SessionController : ApiController
{
    public object Get(string set = null)
    {
        var session = HttpContext.Current.Session;
        if (session == null)
        {
            return new { session = "none" };
        }

        if (set != null)
        {
            session["probe"] = set;
        }

        return new { value = (string)session["probe"] };
    }
}

public class SessionHttpControllerRouteHandler : HttpControllerRouteHandler
{
    protected override IHttpHandler GetHttpHandler(RequestContext requestContext)
    {
        return new SessionControllerHandler(requestContext.RouteData);
    }
}

public class SessionControllerHandler : HttpControllerHandler, IRequiresSessionState
{
    public SessionControllerHandler(RouteData routeData)
        : base(routeData)
    {
    }
}
