using System.Web;

namespace Rehost.WebForms.ScenarioProbes;

// IV11 measured the callback firing inside the SendResponse notification with the status still
// 200 and the head not yet emitted, so a header the callback appends is on the wire.
public sealed class OnSendingHeadersProbe : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var response = context.Response;
        response.ContentType = "text/plain";

        if (context.Request.QueryString["mode"] == "late")
        {
            response.Write("late:");
            response.Flush();
            try
            {
                response.AddOnSendingHeaders(_ => { });
                response.Write("ok");
            }
            catch (Exception refusal)
            {
                response.Write(refusal.GetType().Name);
            }

            return;
        }

        response.AddOnSendingHeaders(sending =>
        {
            sending.Response.AppendHeader("X-On-Sending", sending.Response.StatusCode.ToString());
        });
        response.Write("registered");
    }
}
