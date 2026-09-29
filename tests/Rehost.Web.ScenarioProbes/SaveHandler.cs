using System.Web;
using Rehost.Web.ScenarioProtocol;

namespace Rehost.Web.ScenarioProbes;

public sealed class SaveHandler : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var target = context.Request.QueryString["to"];

        try
        {
            switch (context.Request.QueryString["mode"])
            {
                case "file":
                    var file = context.Request.Files["Picked"];
                    context.Response.AddHeader(
                        ProbeHeaders.Spilled,
                        RequestBodyHandler.IsFileBacked(file.InputStream).ToString());
                    file.SaveAs(target);
                    break;
                case "raw-headers":
                    context.Request.SaveAs(target, includeHeaders: true);
                    break;
                default:
                    context.Request.SaveAs(target, includeHeaders: false);
                    break;
            }

            RequestBodyHandler.WriteResult(context, "saved");
        }
        catch (Exception exception)
        {
            RequestBodyHandler.WriteResult(
                context,
                "error:" + exception.GetType().FullName + ":" + exception.Message);
        }
    }
}
