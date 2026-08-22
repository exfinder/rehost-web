using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// Which row of the merged <handlers> list won the walk is only observable through the body, so
// each marker names itself and nothing else.
public abstract class HandlerMarker : IHttpHandler
{
    protected abstract string Marker { get; }

    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "text/plain";
        context.Response.Write("HANDLED-BY:" + Marker);
    }
}

public sealed class MarkerAHandler : HandlerMarker
{
    protected override string Marker => "A";
}

public sealed class MarkerBHandler : HandlerMarker
{
    protected override string Marker => "B";
}
