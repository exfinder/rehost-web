using System.Net.WebSockets;
using System.Text;
using System.Web;
using System.Web.WebSockets;

namespace Rehost.WebForms.ScenarioProbes;

// The IIS reading's handler (R-WS1..R-WS6): a cookie and a header before AcceptWebSocketRequest,
// a header (and, on request, body bytes) after it, an echo callback whose first frame reports
// what it can see, and the sub-protocol / same-origin variants.
public sealed class WebSocketProbe : IHttpHandler
{
    public bool IsReusable => true;

    public void ProcessRequest(HttpContext context)
    {
        var mode = context.Request.QueryString["mode"] ?? "";
        if (mode == "info")
        {
            context.Response.ContentType = "text/plain";
            string upgrade;
            try
            {
                upgrade = context.IsWebSocketRequest.ToString();
            }
            catch (Exception exception)
            {
                upgrade = "EX:" + exception.GetType().Name + ":" + exception.Message;
            }

            context.Response.Write("IsWebSocketRequest=" + upgrade + "\n");
            return;
        }

        if (!context.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            context.Response.Write("not a websocket request");
            return;
        }

        context.Response.Cookies.Add(new HttpCookie("wscookie", "v1"));
        context.Response.AppendHeader("X-App-Header", "before-accept");
        var options = new AspNetWebSocketOptions();
        if (mode == "sub")
        {
            options.SubProtocol = "chat";
        }

        if (mode == "origin")
        {
            options.RequireSameOrigin = true;
        }

        context.AcceptWebSocketRequest(Echo, options);
        context.Response.AppendHeader("X-After-Accept", "1");
        if (mode == "junk")
        {
            context.Response.Write("body-after-accept");
        }
    }

    private static async Task Echo(AspNetWebSocketContext context)
    {
        var socket = context.WebSocket;
        var current = HttpContext.Current;
        var report = "type=" + socket.GetType().FullName
            + ";sub=" + (socket.SubProtocol ?? "<null>")
            + ";current=" + (current == null ? "null" : "set")
            + ";items=" + context.Items.Count
            + ";origin=" + (context.Origin ?? "<null>")
            + ";user=" + (context.User == null ? "null" : "obj")
            + ";" + Attempt(() => { _ = current!.Response; return "resp=obj"; }, "resp-ex")
            + ";session=" + (current!.Session == null ? "null" : "obj");
        await socket.SendAsync(Encoding.UTF8.GetBytes(report), WebSocketMessageType.Text, true, CancellationToken.None);

        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "bye:" + result.CloseStatus + ":" + result.CloseStatusDescription,
                    CancellationToken.None);
                break;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            if (text == "close-me")
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "server-close", CancellationToken.None);
                break;
            }

            var reply = Encoding.UTF8.GetBytes("echo:" + text + ":" + result.MessageType + ":" + result.EndOfMessage);
            await socket.SendAsync(reply, result.MessageType, true, CancellationToken.None);
        }
    }

    private static string Attempt(Func<string> read, string failure)
    {
        try
        {
            return read();
        }
        catch (Exception exception)
        {
            return failure + "=" + exception.GetType().Name + ":" + exception.Message;
        }
    }
}
