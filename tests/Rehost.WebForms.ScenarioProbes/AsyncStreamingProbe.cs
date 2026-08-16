using System.Threading.Tasks;
using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// Response.FlushAsync from an asynchronous handler, past the first real await so the request is
// off its cancellable period: HttpResponse then takes the worker request's BeginFlush/EndFlush
// arm rather than the synchronous flush. The bytes still reach the client at the flush.
public sealed class AsyncStreamingProbe : HttpTaskAsyncHandler
{
    public override bool IsReusable => true;

    public override async Task ProcessRequestAsync(HttpContext context)
    {
        var delay = int.TryParse(context.Request.QueryString["delay"], out var parsed) ? parsed : 1000;
        context.Response.ContentType = "text/plain";
        context.Response.Write("part1\n");
        await Task.Yield();
        await context.Response.FlushAsync();
        await Task.Delay(delay);
        context.Response.Write("part2\n");
    }
}
