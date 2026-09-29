namespace Rehost.Web.AspNetCore;

using System.Text;
using System.Web.Configuration;
using System.Web.Hosting;

// Framework wrote every response header name and value in <globalization responseHeaderEncoding>
// (UTF-8 by default; reading R4: iso-8859-1 put Latin-1 bytes on the wire with '?' for what it
// cannot express, which Encoding.Latin1 also does). Kestrel encodes headers through a process-wide
// selector, and one application runs per process, so the application's setting is the selector's
// answer. The configuration is readable only once the application has activated; a response the
// host writes before that (the front-door 403/413) takes Framework's default, and the value is
// held for the process from the first activated response on.
internal static class ResponseHeaderEncoding
{
    private static Encoding? _configured;

    internal static Encoding Select(string headerName)
    {
        _ = headerName;
        if (_configured != null)
        {
            return _configured;
        }

        if (!HostingEnvironment.IsHosted)
        {
            return Encoding.UTF8;
        }

        var globalization = RuntimeConfig.GetAppLKGConfig().Globalization;
        if (globalization == null)
        {
            return Encoding.UTF8;
        }

        return _configured = globalization.ResponseHeaderEncoding;
    }
}
