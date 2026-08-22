using System.Web;
using System.Web.SessionState;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class ReadOnlySessionProbe : IHttpHandler, IReadOnlySessionState
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var request = context.Request;
        var session = context.Session!;

        if (request.QueryString["mode"] == "hold")
        {
            var tag = request.QueryString["tag"];
            Witness.Record(WitnessProtocol.SessionEntered + tag);
            Thread.Sleep(int.Parse(
                request.QueryString["ms"]!,
                System.Globalization.CultureInfo.InvariantCulture));
            Witness.Record(WitnessProtocol.SessionExited + tag);
        }

        if (request.QueryString["write"] != null)
        {
            try
            {
                session["v"] = request.QueryString["write"];
                context.Response.Write("write=ok\n");
            }
            catch (Exception exception)
            {
                context.Response.Write("write=threw:" + exception.GetType().Name + "\n");
            }
        }

        SessionReport.Write(context, session);
    }
}
