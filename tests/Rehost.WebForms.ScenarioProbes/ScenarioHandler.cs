using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// Resolved from the fixture's bin directory through configured handler mapping, the way the
// first-slice probes are. The response body is not what scenarios assert on; reaching the handler
// at all is what proves the request completed through the pipeline.
public sealed class ScenarioHandler : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        ScenarioJournal.Record("handler");
        context.Response.StatusCode = 200;
        context.Response.Write("scenario");
    }
}
