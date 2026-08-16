using System;
using System.Text;
using System.Web;

namespace Rehost.WebForms.Parity.Probes;

// Prints every path-shaped view a handler has of its request, one per line, so a wire reading
// on Framework and the port's own answer compare as text. Each view is read on its own: a
// view that throws reports the exception where the value would be, and the rest still print.
public sealed class PathProbeHandler : IHttpHandler
{
    public bool IsReusable => true;

    public void ProcessRequest(HttpContext context)
    {
        var request = context.Request;
        var text = new StringBuilder();
        Append(text, "path", () => request.Path);
        Append(text, "rawurl", () => request.RawUrl);
        Append(text, "filepath", () => request.FilePath);
        Append(text, "pathinfo", () => request.PathInfo);
        Append(text, "currentexec", () => request.CurrentExecutionFilePath);
        Append(text, "apprel", () => request.AppRelativeCurrentExecutionFilePath);
        Append(text, "absolute", () => request.Url.AbsolutePath);
        Append(text, "physical", () => request.PhysicalPath);
        Append(text, "mapdot", () => context.Server.MapPath("."));
        Append(text, "apppath", () => request.ApplicationPath);
        Append(text, "query", () => request.Url.Query);

        var body = Encoding.UTF8.GetBytes(text.ToString());
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.OutputStream.Write(body, 0, body.Length);
    }

    private static void Append(StringBuilder text, string name, Func<string> read)
    {
        string value;
        try
        {
            value = read() ?? "<null>";
        }
        catch (Exception ex)
        {
            value = "EX " + ex.GetType().Name + ": " + ex.Message;
        }

        text.Append(name).Append('=').Append(value).Append('\n');
    }
}
