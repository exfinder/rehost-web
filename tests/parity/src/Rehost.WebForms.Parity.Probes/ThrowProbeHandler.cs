using System;
using System.Web;

namespace Rehost.WebForms.Parity.Probes;

public sealed class ThrowProbeHandler : IHttpHandler
{
    public bool IsReusable => false;

    public void ProcessRequest(HttpContext context)
    {
        ProbeJournal.Record(context, "handler.throw");
        throw new InvalidOperationException("Probe handler failed deliberately.");
    }
}
