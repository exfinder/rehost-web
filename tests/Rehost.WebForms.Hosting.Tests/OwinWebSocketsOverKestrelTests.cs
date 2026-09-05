using System.Net.WebSockets;
using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The OWIN WebSocket path the integrated identity opened: Katana advertises websocket.Version at
// startup (IV23), hands middleware websocket.Accept, and OwinCallContext carries the accept into
// System.Web's AcceptWebSocketRequest. The fixture middleware echoes one frame over the raw OWIN
// environment delegates and closes.
public sealed class OwinWebSocketsOverKestrelTests(FriendlyUrlsLiveScenario scenario)
    : IClassFixture<FriendlyUrlsLiveScenario>, IDisposable
{
    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromSeconds(20));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Owin_Middleware_Accepts_The_Handshake_And_Frames_Echo()
    {
        using var socket = new ClientWebSocket();
        var uri = new UriBuilder(scenario.Address) { Scheme = "ws", Path = "/owin-ws" }.Uri;
        await socket.ConnectAsync(uri, _deadline.Token);

        await socket.SendAsync(
            "hello"u8.ToArray(), WebSocketMessageType.Text, true, _deadline.Token);
        var buffer = new byte[1024];
        var echoed = await socket.ReceiveAsync(buffer, _deadline.Token);

        echoed.MessageType.ShouldBe(WebSocketMessageType.Text);
        Encoding.UTF8.GetString(buffer, 0, echoed.Count).ShouldBe("owin-echo:1:hello");

        var close = await socket.ReceiveAsync(buffer, _deadline.Token);
        close.MessageType.ShouldBe(WebSocketMessageType.Close);
        close.CloseStatus.ShouldBe(WebSocketCloseStatus.NormalClosure);
        close.CloseStatusDescription.ShouldBe("owin-bye");
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", _deadline.Token);
    }
}
