using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class BrowserCapabilitiesProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var browser = context.Request.Browser;
        var browsers = string.Join(",", browser.Browsers.Cast<string>());
        context.Response.ContentType = "text/plain";
        context.Response.Write($"browsers={browsers}\nprobe={browser["rehostProbe"] ?? "none"}\n");
    }
}
