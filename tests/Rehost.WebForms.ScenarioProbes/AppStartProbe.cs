using System;
using System.IO;
using System.Threading;
using System.Web;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioProbes;

// The run number in the fault text separates a latched failure from a re-run.
public static class AppStartProbe
{
    private static int _runs;

    public static void Enter()
    {
        var run = Interlocked.Increment(ref _runs);
        var marker = Environment.GetEnvironmentVariable(AppStartProtocol.FaultMarkerVariable);

        if (!string.IsNullOrEmpty(marker) && File.Exists(marker))
        {
            using (var gate = ScenarioGate.Open())
            {
                gate.ArriveAndWait();
            }

            throw new InvalidOperationException($"{AppStartProtocol.FaultText}{run}");
        }
    }
}

// The recycle cause that reaches the hosting seam without the initialization latch.
public sealed class UnloadProbe : IHttpHandler
{
    public bool IsReusable
    {
        get { return false; }
    }

    public void ProcessRequest(HttpContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.Write("unloading");
        context.Response.Flush();
        HttpRuntime.UnloadAppDomain();
    }
}
