using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// The application's half of the custom-header readings: a name the configured collection also
// carries, appended or removed from inside the handler (CH5, CH8).
public sealed class CustomHeadersProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var response = context.Response;
        response.ContentType = "text/plain";

        switch (context.Request.QueryString["case"])
        {
            case "append":
                response.AppendHeader("X-Fixture", "app");
                break;
            case "remove":
                response.AppendHeader("X-Fixture", "app");
                response.Headers.Remove("X-Fixture");
                break;
        }

        response.Write("custom-headers");
    }
}
