using System.Net;
using System.Web;

namespace System.Web.Services.Protocols;

// The imported RemoteDebugger is a Windows COM channel for VS step-into; closed
// gates are also Framework's behavior whenever that COM activation fails.
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
