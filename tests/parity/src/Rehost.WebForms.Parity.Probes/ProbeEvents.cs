using System.Web;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.Probes;

// Probes run on whatever thread the pipeline gives them, so they identify their request from a
// header the recording worker request carries rather than from ambient context.
internal static class ProbeEvents
{
    internal static void Record(HttpContext context, string value)
    {
        PipelineEvents.Record(NameOf(context), value);
    }

    internal static string? NameOf(HttpContext context)
    {
        return context?.Request.Headers[PipelineEvents.RequestHeaderName];
    }
}
