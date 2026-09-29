using System.Text;
using System.Web;
using Rehost.Web.Parity.Contracts;

namespace Rehost.Web.Parity.Probes;

public sealed class SyncProbeHandler : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        ProbeEvents.Record(context, "handler.process-request");

        var body = Encoding.UTF8.GetBytes("oracle-ok");
        context.Response.StatusCode = 201;
        context.Response.StatusDescription = "Oracle Created";
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.AppendHeader("X-Framework-Oracle", "cold-sync");
        context.Response.OutputStream.Write(body, 0, body.Length);
    }
}
