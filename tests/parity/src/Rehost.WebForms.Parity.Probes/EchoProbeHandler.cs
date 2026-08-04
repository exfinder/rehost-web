using System;
using System.Text;
using System.Web;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.Probes;

// Concurrent requests that are indistinguishable cannot detect a crossed context, so each one
// writes back the identity it was given. A response carrying someone else's name is a mismatch.
public sealed class EchoProbeHandler : IHttpHandler
{
    private static readonly TimeSpan BarrierTimeout = TimeSpan.FromSeconds(5);

    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        var name = ProbeJournal.NameOf(context) ?? "";
        ProbeJournal.Record(context, "handler.echo");

        if (!ParityBarrier.Arrive(BarrierTimeout))
        {
            ProbeJournal.Record(context, "handler.barrier-timeout");
        }

        var body = Encoding.UTF8.GetBytes("echo:" + name);
        context.Response.StatusCode = 200;
        context.Response.StatusDescription = "OK";
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.OutputStream.Write(body, 0, body.Length);
    }
}
