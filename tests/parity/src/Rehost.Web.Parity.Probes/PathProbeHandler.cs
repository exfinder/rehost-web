using System;
using System.Text;
using System.Web;

namespace Rehost.Web.Parity.Probes;

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
        Append(text, "url", () => request.Url.ToString());
        Append(text, "secure", () => request.IsSecureConnection ? "true" : "false");
        Append(text, "servername", () => request.ServerVariables["SERVER_NAME"] ?? "<null>");
        Append(text, "serverport", () => request.ServerVariables["SERVER_PORT"] ?? "<null>");
        Append(text, "https", () => request.ServerVariables["HTTPS"] ?? "<null>");
        Append(text, "applmd", () => request.ServerVariables["APPL_MD_PATH"] ?? "<null>");
        Append(text, "software", () => request.ServerVariables["SERVER_SOFTWARE"] ?? "<null>");
        Append(text, "remoteaddr", () => request.UserHostAddress);
        Append(text, "xprobe", () => CharCodes(request.Headers["X-Probe"]));

        context.Response.AppendHeader("X-Accent", "caf\u00e9");

        var body = Encoding.UTF8.GetBytes(text.ToString());
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.OutputStream.Write(body, 0, body.Length);
    }

    // Header bytes as the application saw them, one code per character, so a decoding difference
    // survives the response's own encoding.
    private static string CharCodes(string? value)
    {
        if (value == null)
        {
            return "<null>";
        }

        var codes = new StringBuilder();
        foreach (var c in value)
        {
            if (codes.Length > 0)
            {
                codes.Append(',');
            }

            codes.Append((int)c);
        }

        return codes.ToString();
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
