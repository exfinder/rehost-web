using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// The pipeline identity a running application reads, in the two spellings IV16 measured.
public sealed class RuntimeIdentityProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "text/plain";
        context.Response.Write(
            $"integrated={HttpRuntime.UsingIntegratedPipeline};iis={HttpRuntime.IISVersion}");
    }
}
