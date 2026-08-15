using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// Declares no session marker interface, so the module must leave HttpContext.Session null.
public sealed class PlainSessionProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context) =>
        SessionReport.Write(context, context.Session);
}
