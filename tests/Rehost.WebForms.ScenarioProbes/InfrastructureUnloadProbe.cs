using System.Web;
using Microsoft.Web.Infrastructure;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class InfrastructureUnloadProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "text/plain";
        context.Response.Write("unloading");
        InfrastructureHelper.UnloadAppDomain();
    }
}
