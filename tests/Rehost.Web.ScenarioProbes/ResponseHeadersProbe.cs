using System.Text;
using System.Web;

namespace Rehost.Web.ScenarioProbes;

// One case per Response.Headers reading (H1-H16 in the IIS integration plan), applying the
// stimulus and dumping the collection, the fields, and the status while still inside the
// handler, exactly as the IIS probe that took the readings did.
public sealed class ResponseHeadersProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var response = context.Response;
        var dump = new StringBuilder();

        switch (context.Request.QueryString["case"])
        {
            case "contenttype":
                response.ContentType = "text/plain";
                break;
            case "cookie":
                response.Cookies.Add(new HttpCookie("a", "1"));
                break;
            case "cookie2":
                response.Cookies.Add(new HttpCookie("a", "1"));
                response.Cookies.Add(new HttpCookie("b", "2"));
                break;
            case "redirect":
                response.Redirect("/x", false);
                break;
            case "cache":
                response.Cache.SetCacheability(HttpCacheability.Public);
                break;
            case "cachemax":
                response.Cache.SetCacheability(HttpCacheability.Public);
                response.Cache.SetMaxAge(TimeSpan.FromSeconds(30));
                break;
            case "append":
                response.AppendHeader("X-Custom", "v1");
                response.AppendHeader("X-Custom", "v2");
                break;
            case "setloc":
                response.Headers.Set("Location", "/y");
                break;
            case "setloc302":
                response.Headers.Set("Location", "/y");
                response.StatusCode = 302;
                break;
            case "setct":
                response.Headers.Set("Content-Type", "text/csv");
                break;
            case "addct":
                response.Headers.Add("Content-Type", "text/csv");
                break;
            case "setcc":
                response.Headers.Set("Cache-Control", "no-store");
                break;
            case "rmcookie":
                response.Cookies.Add(new HttpCookie("a", "1"));
                response.Headers.Remove("Set-Cookie");
                break;
            case "rmcookie2":
                response.Cookies.Add(new HttpCookie("a", "1"));
                response.Headers.Remove("Set-Cookie");
                response.Cookies.Add(new HttpCookie("b", "2"));
                break;
            case "rmcache":
                response.Headers.Remove("Cache-Control");
                response.Headers.Remove("X-AspNet-Version");
                break;
            case "rmct":
                response.Headers.Remove("Content-Type");
                break;
            case "rmloc":
                response.Redirect("/x", false);
                response.Headers.Remove("Location");
                break;
            case "rmappend":
                response.AppendHeader("X-Custom", "v1");
                response.Headers.Remove("X-Custom");
                break;
            case "addsetcookie":
                response.Headers.Add("Set-Cookie", "z=9; path=/");
                break;
            case "afterflush":
                response.AppendHeader("X-Custom", "v1");
                response.Write("flushed\n");
                response.Flush();
                Attempt(dump, "late add", () => response.Headers.Add("X-Late", "v"));
                Attempt(dump, "late remove", () => response.Headers.Remove("X-Custom"));
                Attempt(dump, "late get", () => dump.Append("late get: ")
                    .Append(response.Headers["Cache-Control"])
                    .Append('\n'));
                break;
            case "clear":
                response.AppendHeader("X-Custom", "v1");
                response.ClearHeaders();
                break;
        }

        Describe(response, dump);
        response.Write(dump.ToString());
    }

    private static void Describe(HttpResponse response, StringBuilder dump)
    {
        var headers = response.Headers;

        dump.Append("Count=").Append(headers.Count).Append('\n');

        foreach (var key in headers.AllKeys)
        {
            foreach (var value in headers.GetValues(key) ?? [])
            {
                dump.Append("H ").Append(key).Append('=').Append(value).Append('\n');
            }
        }

        dump.Append("ContentType=").Append(response.ContentType).Append('\n');
        dump.Append("RedirectLocation=").Append(response.RedirectLocation ?? "null").Append('\n');
        dump.Append("StatusCode=").Append(response.StatusCode).Append('\n');
    }

    // The reading recorded which of the three post-flush calls refused, so the outcome is the
    // observation: a throw here would replace it with a 500.
    private static void Attempt(StringBuilder dump, string label, Action action)
    {
        try
        {
            action();
            if (label != "late get")
            {
                dump.Append(label).Append(": ok\n");
            }
        }
        catch (Exception exception)
        {
            dump.Append(label).Append(": ").Append(exception.GetType().FullName)
                .Append(": ").Append(exception.Message).Append('\n');
        }
    }
}
