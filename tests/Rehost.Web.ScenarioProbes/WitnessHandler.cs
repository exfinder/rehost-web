using System.Web;

namespace Rehost.Web.ScenarioProbes;

public sealed class WitnessHandler : IHttpHandler
{
    public bool IsReusable => true;

    public void ProcessRequest(HttpContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/plain";
        context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
        context.Response.Write(string.Join("\n", Witness.Snapshot()));
    }
}
