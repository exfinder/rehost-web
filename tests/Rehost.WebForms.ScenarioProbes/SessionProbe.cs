using System.Globalization;
using System.Web;
using System.Web.SessionState;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class SessionProbe : IHttpHandler, IRequiresSessionState
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var request = context.Request;
        var session = context.Session!;

        switch (request.QueryString["mode"])
        {
            case "write":
                session["v"] = request.QueryString["v"];
                break;

            case "abandon":
                session.Abandon();
                break;

            case "hold":
                var tag = request.QueryString["tag"];
                Witness.Record(WitnessProtocol.SessionEntered + tag);
                Thread.Sleep(int.Parse(
                    request.QueryString["ms"]!,
                    CultureInfo.InvariantCulture));
                Witness.Record(WitnessProtocol.SessionExited + tag);
                break;
        }

        SessionReport.Write(context, session);
    }
}
