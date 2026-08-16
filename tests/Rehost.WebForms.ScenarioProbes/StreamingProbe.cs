using System.Threading;
using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// Response.Flush mid-request, in the shapes of the IIS readings R-S1..R-S5: flush/delay/flush,
// End after a flush, TransmitFile between flushes, a header change after a flush, and an
// unhandled error after a flush.
public sealed class StreamingProbe : IHttpHandler
{
    public bool IsReusable => true;

    public void ProcessRequest(HttpContext context)
    {
        var response = context.Response;
        response.ContentType = "text/plain";
        var delay = int.TryParse(context.Request.QueryString["delay"], out var parsed) ? parsed : 1000;

        switch (context.Request.QueryString["case"])
        {
            case "end":
                response.Write("part1\n");
                response.Flush();
                Thread.Sleep(delay);
                response.Write("part2\n");
                response.End();
                response.Write("never\n");
                break;
            case "file":
                response.Write("part1\n");
                response.Flush();
                Thread.Sleep(delay);
                response.TransmitFile("~/stream/data.txt");
                response.Write("part2\n");
                response.Flush();
                response.Write("part3\n");
                break;
            case "lateheader":
                response.Write("part1\n");
                response.Flush();
                Attempt(response, "append", () => response.AppendHeader("X-Late", "1"));
                Attempt(response, "status", () => response.StatusCode = 404);
                Attempt(response, "cookie", () => response.Cookies.Add(new HttpCookie("late", "1")));
                response.Write("clientconnected=" + response.IsClientConnected + "\n");
                break;
            case "error":
                response.Write("part1\n");
                response.Flush();
                throw new InvalidOperationException("boom after flush");
            case "async":
                response.Write("part1\n");
                response.FlushAsync().GetAwaiter().GetResult();
                Thread.Sleep(delay);
                response.Write("part2\n");
                break;
            default:
                response.Write("part1\n");
                response.Flush();
                Thread.Sleep(delay);
                response.Write("part2\n");
                response.Flush();
                Thread.Sleep(delay / 4);
                response.Write("part3\n");
                break;
        }
    }

    private static void Attempt(HttpResponse response, string name, Action action)
    {
        try
        {
            action();
            response.Write(name + "-ok\n");
        }
        catch (Exception exception)
        {
            response.Write(name + "-ex:" + exception.GetType().Name + ":" + exception.Message + "\n");
        }
    }
}
