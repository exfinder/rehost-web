using System.Globalization;
using System.Web;
using System.Web.SessionState;
using Rehost.Web.ScenarioProtocol;

namespace Rehost.Web.ScenarioProbes;

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
                using (var gate = ScenarioGate.Open(request.QueryString["gate"]))
                {
                    Witness.Record($"{WitnessProtocol.SessionEntered}{tag}");
                    if (gate.Armed)
                    {
                        gate.ArriveAndWait();
                    }
                    else
                    {
                        Thread.Sleep(int.Parse(
                            request.QueryString["ms"]!,
                            CultureInfo.InvariantCulture));
                    }

                    Witness.Record($"{WitnessProtocol.SessionExited}{tag}");
                }

                break;
        }

        SessionReport.Write(context, session);
    }
}
