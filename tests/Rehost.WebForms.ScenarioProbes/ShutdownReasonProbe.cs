using System.Diagnostics;
using System.Web;
using System.Web.Hosting;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioProbes;

// Answers only once shutdown has begun, so a 200 carrying a reason left the process after it.
public sealed class ShutdownReasonProbe : IHttpHandler
{
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(20);

    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        using (var gate = ScenarioGate.Open())
        {
            gate.ArriveAndWait();
        }

        var waited = Stopwatch.StartNew();
        while (HostingEnvironment.ShutdownReason == ApplicationShutdownReason.None
            && waited.Elapsed < ShutdownWait)
        {
            Thread.Sleep(10);
        }

        context.Response.ContentType = "text/plain";
        context.Response.Write(HostingEnvironment.ShutdownReason.ToString());
    }
}
