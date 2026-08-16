using System.Net;
using System.Net.WebSockets;
using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// AcceptWebSocketRequest over Kestrel against the IIS + Framework readings (R-WS1..R-WS6,
// research/websockets-and-streaming.md): the 101 carries the application's cookies and headers,
// the callback sees Framework's slimmed context over an AspNetWebSocket, frames echo, both close
// directions complete, sub-protocol and same-origin refusals keep Framework's status and text.
public sealed class WebSocketsOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>, IDisposable
{
    // A handshake or frame answers over loopback in well under this; longer means the server
    // wedged (the WebSocket callback runs the classic pipeline), and a wedge must fail its test
    // rather than hang the run on a ClientWebSocket receive that never returns.
    private readonly CancellationTokenSource _deadline =
        new(TimeSpan.FromSeconds(20));

    public void Dispose() => _deadline.Dispose();

    private Uri WsUri(string query) => new UriBuilder(scenario.Address)
    {
        Scheme = "ws",
        Path = "/ws/echo",
        Query = query,
    }.Uri;

    private async Task<string> ReceiveTextAsync(ClientWebSocket socket)
    {
        var buffer = new byte[8192];
        var result = await socket.ReceiveAsync(buffer, _deadline.Token);
        result.MessageType.ShouldBe(WebSocketMessageType.Text);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    [Fact]
    public async Task The_Handshake_Carries_The_Applications_Cookie_And_Headers_And_Frames_Echo()
    {
        using var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        await socket.ConnectAsync(WsUri(""), _deadline.Token);

        socket.HttpStatusCode.ShouldBe(HttpStatusCode.SwitchingProtocols);
        socket.HttpResponseHeaders!["Set-Cookie"].ShouldContain("wscookie=v1; path=/");
        socket.HttpResponseHeaders["X-App-Header"].ShouldContain("before-accept");
        socket.HttpResponseHeaders["X-After-Accept"].ShouldContain("1");
        socket.HttpResponseHeaders.ContainsKey("Content-Length").ShouldBeFalse();

        // IIS reported user=obj: its DefaultAuthenticationModule gives an anonymous principal, a
        // module the shipped root configuration does not carry yet (shipped-modules follow-up).
        var report = await ReceiveTextAsync(socket);
        report.ShouldBe(
            "type=System.Web.WebSockets.AspNetWebSocket;sub=<null>;current=set;items=kept;origin=<null>;user=null;"
            + "resp-ex=HttpException:Response is not available in this context.;session=null");

        await socket.SendAsync("hello"u8.ToArray(), WebSocketMessageType.Text, true, _deadline.Token);
        (await ReceiveTextAsync(socket)).ShouldBe("echo:hello:Text:True");

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client-bye", _deadline.Token);
        socket.State.ShouldBe(WebSocketState.Closed);
        socket.CloseStatus.ShouldBe(WebSocketCloseStatus.NormalClosure);
        socket.CloseStatusDescription.ShouldBe("bye:NormalClosure:client-bye");
    }

    [Fact]
    public async Task A_Server_Initiated_Close_Completes()
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(WsUri(""), _deadline.Token);
        await ReceiveTextAsync(socket);

        await socket.SendAsync("close-me"u8.ToArray(), WebSocketMessageType.Text, true, _deadline.Token);
        var close = await socket.ReceiveAsync(new byte[16], _deadline.Token);

        close.MessageType.ShouldBe(WebSocketMessageType.Close);
        close.CloseStatus.ShouldBe(WebSocketCloseStatus.NormalClosure);
        close.CloseStatusDescription.ShouldBe("server-close");
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", _deadline.Token);
    }

    [Fact]
    public async Task A_Negotiated_Sub_Protocol_Is_On_The_Handshake_And_The_Socket()
    {
        using var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol("chat");
        socket.Options.AddSubProtocol("superchat");
        await socket.ConnectAsync(WsUri("mode=sub"), _deadline.Token);

        socket.SubProtocol.ShouldBe("chat");
        (await ReceiveTextAsync(socket)).ShouldContain(";sub=chat;");
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", _deadline.Token);
    }

    // Body bytes written after the accept were raw noise between IIS's 101 and the first frame
    // (R-WS2); here they are dropped and the socket parses cleanly.
    [Fact]
    public async Task Body_Written_After_The_Accept_Is_Discarded()
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(WsUri("mode=junk"), _deadline.Token);

        (await ReceiveTextAsync(socket)).ShouldStartWith("type=System.Web.WebSockets.AspNetWebSocket");
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", _deadline.Token);
    }

    [Fact]
    public async Task Refusals_Keep_Frameworks_Status_And_Text()
    {
        // sub-protocol the client did not offer: 500 ArgumentException (R-WS4)
        var mismatch = await RawHandshakeAsync("mode=sub", "Sec-WebSocket-Protocol: other");
        mismatch.ShouldStartWith("HTTP/1.1 500 ");
        mismatch.ShouldContain("The sub-protocol &#39;chat&#39; cannot be negotiated for this request.");

        // RequireSameOrigin without / with a foreign Origin: 403 (R-WS5)
        (await RawHandshakeAsync("mode=origin", null)).ShouldStartWith("HTTP/1.1 403 ");
        (await RawHandshakeAsync("mode=origin", "Origin: http://evil.example")).ShouldStartWith("HTTP/1.1 403 ");
        (await RawHandshakeAsync("mode=origin", "Origin: http://" + scenario.Address.Authority))
            .ShouldStartWith("HTTP/1.1 101 ");

        // a plain GET is not a WebSocket request (R-WS6)
        var info = await scenario.Client.GetAsync("/ws/echo?mode=info");
        info.Text.ShouldBe("IsWebSocketRequest=False\n");
        (await scenario.Client.GetAsync("/ws/echo")).StatusCode.ShouldBe(400);
    }

    // Framework refuses the question during BeginRequest; the classic pipeline reports the same.
    [Fact]
    public async Task Asking_During_Begin_Request_Is_Refused()
    {
        var response = await scenario.Client.GetAsync("/ws/echo?mode=info&ws-begin=1");

        response.Header("X-Ws-Begin").ShouldBe(
            "InvalidOperationException:This method cannot be called during or before BeginRequest.");
    }

    private async Task<string> RawHandshakeAsync(string query, string? extraHeader)
    {
        var request = $"""
            GET /ws/echo?{query} HTTP/1.1
            Host: {scenario.Address.Authority}
            Upgrade: websocket
            Connection: Upgrade
            Sec-WebSocket-Key: {Convert.ToBase64String(new byte[16])}
            Sec-WebSocket-Version: 13
            {extraHeader}


            """.ReplaceLineEndings("\r\n");
        request = System.Text.RegularExpressions.Regex.Replace(request, "(\r\n){3,}$", "\r\n\r\n");
        var raw = await RawSocketProbe.SendRawUntilQuietAsync(scenario.Address, Encoding.ASCII.GetBytes(request));
        return Encoding.UTF8.GetString(raw);
    }
}
