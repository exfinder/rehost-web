using System.Text;
using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// IV7-IV9: the request header collection is writable, and its mirrors follow only where the
// mirror collection already existed (server variables) or the derived value was not yet cached
// (Request.Url). Each mode applies the same mutation and reports a different set of mirrors.
// The mirror modes read Referer, not User-Agent: GetSimpleServerVar answers HTTP_USER_AGENT from
// the typed accessor without ever populating the collection.
public sealed class RequestHeadersProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var request = context.Request;
        var dump = new StringBuilder();
        context.Response.ContentType = "text/plain";

        switch (request.QueryString["mode"])
        {
            case "mirror":
                Line(dump, "before-referer", request.ServerVariables["HTTP_REFERER"]);
                Mutate(request, dump);
                Line(dump, "sv-probe", request.ServerVariables["HTTP_X_PROBE"]);
                Line(dump, "sv-referer", request.ServerVariables["HTTP_REFERER"]);
                break;

            case "lazy":
                Line(dump, "before-host", request.Url.Host);
                Mutate(request, dump);
                Line(dump, "sv-referer", request.ServerVariables["HTTP_REFERER"]);
                Line(dump, "url-host", request.Url.Host);
                break;

            default:
                Mutate(request, dump);
                Line(dump, "probe", request.Headers["X-Probe"]);
                Line(dump, "multi", request.Headers["X-Multi"]);
                Line(dump, "agent-header", request.Headers["User-Agent"]);
                Line(dump, "agent-typed", request.UserAgent);
                Line(dump, "url-authority", request.Url.Authority);
                break;
        }

        context.Response.Write(dump.ToString());
    }

    private static void Mutate(HttpRequest request, StringBuilder dump)
    {
        var headers = request.Headers;

        headers.Set("X-Probe", "p");
        headers.Add("X-Multi", "a");
        headers.Add("X-Multi", "b");
        headers.Remove("User-Agent");
        headers.Remove("Referer");

        try
        {
            headers.Clear();
            Line(dump, "clear", "ok");
        }
        catch (Exception refusal)
        {
            Line(dump, "clear", refusal.GetType().Name);
        }

        headers.Set("Host", "rewritten.example");
    }

    private static void Line(StringBuilder dump, string label, string? value) =>
        dump.Append(label).Append('=').Append(value ?? "null").Append('\n');
}
