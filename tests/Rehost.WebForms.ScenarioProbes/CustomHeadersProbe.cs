using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

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
