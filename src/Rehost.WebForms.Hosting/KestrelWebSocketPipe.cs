namespace Rehost.WebForms.Hosting;

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web.WebSockets;
using Microsoft.AspNetCore.Http;

// The four operations AspNetWebSocket needs from a transport, which IIS supplied natively, over
// the WebSocket ASP.NET Core's middleware accepted. AspNetWebSocket owns the state machine and
// the cancellation; this only moves fragments.
internal sealed class KestrelWebSocketPipe(WebSocket socket, HttpContext context) : IWebSocketPipe
{
    // The rude-close path (AspNetWebSocket.Abort). After a graceful close this also fires from
    // the handoff's final AbortAsync, aborting an already-closed connection — a no-op on the
    // wire, matching Framework's teardown.
    public void CloseTcpConnection()
    {
        socket.Abort();
        context.Abort();
    }

    public Task<WebSocketReceiveResult> ReadFragmentAsync(ArraySegment<byte> buffer)
    {
        return socket.ReceiveAsync(buffer, CancellationToken.None);
    }

    public Task WriteCloseFragmentAsync(WebSocketCloseStatus closeStatus, string statusDescription)
    {
        return socket.CloseOutputAsync(closeStatus, statusDescription, CancellationToken.None);
    }

    public Task WriteFragmentAsync(ArraySegment<byte> buffer, bool isUtf8Encoded, bool isFinalFragment)
    {
        return socket.SendAsync(
            buffer,
            isUtf8Encoded ? WebSocketMessageType.Text : WebSocketMessageType.Binary,
            isFinalFragment,
            CancellationToken.None);
    }
}
