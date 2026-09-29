using System.Web;

namespace Rehost.Web.ScenarioProbes;

// IV22: an integrated pool hid HttpContext.Current.Request and .Response during Application_Start,
// the application's own Init() and a request instance's module Init. Each site records what the
// two reads did; the report handler serves the record back.
public static class LifecycleProbe
{
    private static readonly Lock Gate = new();
    private static readonly List<string> Notes = [];
    private static readonly HashSet<string> Seen = [];

    // Init and module Init run for every application instance the factory builds; only the first
    // is the claim, and recording them all would grow without bound.
    public static void Note(string where)
    {
        var current = HttpContext.Current;
        var entry = where
            + ":current=" + (current == null ? "null" : "set")
            + ";request=" + Attempt(() => current!.Request.Path)
            + ";response=" + Attempt(() => current!.Response.ContentType);

        lock (Gate)
        {
            if (Seen.Add(where))
            {
                Notes.Add(entry);
            }
        }
    }

    public static string[] Snapshot()
    {
        lock (Gate)
        {
            return [.. Notes];
        }
    }

    private static string Attempt(Func<string> read)
    {
        try
        {
            return "ok:" + read();
        }
        catch (Exception refusal)
        {
            return refusal.GetType().Name + ":" + refusal.Message;
        }
    }
}

public sealed class LifecycleModule : IHttpModule
{
    public void Init(HttpApplication application) => LifecycleProbe.Note("module-init");

    public void Dispose()
    {
    }
}

public sealed class LifecycleReportProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "text/plain";
        context.Response.Write(string.Join("\n", LifecycleProbe.Snapshot()));
    }
}
