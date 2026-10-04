using System.Reflection;
using System.Web;

namespace Rehost.Web.ScenarioProbes;

public sealed class ProbingProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "text/plain";
        context.Response.Write(Assembly.Load("Rehost.Web.ScenarioProbes.Dynamic").Location);
    }
}
