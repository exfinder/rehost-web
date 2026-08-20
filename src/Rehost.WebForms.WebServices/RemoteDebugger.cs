using System.Net;
using System.Web;

namespace System.Web.Services.Protocols;

// Stands in for the imported RemoteDebugger, whose Visual Studio step-into
// channel is a Windows COM server; the gates are permanently closed here, which
// is also Framework's behavior whenever the COM activation fails.
internal class RemoteDebugger
{
    internal RemoteDebugger()
    {
    }

    internal static bool IsClientCallOutEnabled() => false;

    internal static bool IsServerCallInEnabled(ServerProtocol protocol, out string stringBuffer)
    {
        stringBuffer = null;
        return false;
    }

    internal void NotifyClientCallOut(WebRequest request)
    {
    }

    internal void NotifyClientCallReturn(WebResponse response)
    {
    }

    internal void NotifyServerCallEnter(ServerProtocol protocol, string stringBuffer)
    {
    }

    internal void NotifyServerCallExit(HttpResponse response)
    {
    }
}
