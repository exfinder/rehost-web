using System.Text;
using System.Web;
using CoreParity.Contracts;

namespace CoreParity.Probes;

public sealed class SyncProbeHandler : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        PipelineEventJournal.Record("handler.process-request");

        var body = Encoding.UTF8.GetBytes("oracle-ok");
        context.Response.StatusCode = 201;
        context.Response.StatusDescription = "Oracle Created";
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.AppendHeader("X-Framework-Oracle", "cold-sync");
        context.Response.OutputStream.Write(body, 0, body.Length);
    }
}
