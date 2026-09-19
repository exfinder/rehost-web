using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class BaseDirectoryProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "text/plain";
        context.Response.Write(AppDomain.CurrentDomain.BaseDirectory);
    }
}
