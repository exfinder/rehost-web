using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Shouldly;
using Rehost.WebForms.ScenarioProtocol;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class RequestBodyOverKestrelTests(
    BodyLiveScenario scenario,
    AbortLiveScenario aborts,
    ITestOutputHelper output)
    : IClassFixture<BodyLiveScenario>, IClassFixture<AbortLiveScenario>
{
    private static readonly TimeSpan AbortDetectionBudget = TimeSpan.FromSeconds(60);

    private ScenarioClient Client => scenario.Client;

    // The interim 100 of the expect-continue handshake is asserted explicitly by the abort tests,
    // whose raw socket observes it; here HttpClient drives the handshake and a described body
    // proves the upload crossed it.
    [Fact]
    public async Task Fixed_And_Chunked_Bodies_Reach_All_Raw_Request_Surfaces()
    {
        var surfaces = new (string Probe, string Mode, BodyFraming Framing, bool ExpectContinue)[]
        {
            ("fixed-input", "input", BodyFraming.Fixed, false),
            ("fixed-binary", "binary", BodyFraming.Fixed, false),
            ("fixed-buffered", "buffered", BodyFraming.Fixed, false),
            ("fixed-bufferless", "bufferless", BodyFraming.Fixed, false),
            ("chunked-bufferless", "bufferless", BodyFraming.DelayedChunked, false),
            ("chunked-apm", "apm", BodyFraming.DelayedChunked, false),
            ("expect-continue", "bufferless", BodyFraming.Fixed, true),
        };

        foreach (var surface in surfaces)
        {
            var response = await Client.PostBodyAsync(
                ProbePaths.Body + "?mode=" + surface.Mode,
                Encoding.UTF8.GetBytes("body:" + surface.Probe),
                surface.Framing,
                expectContinue: surface.ExpectContinue);

            response.StatusCode.ShouldBe(200, surface.Probe);
            response.Text.ShouldBe(Describe("body:" + surface.Probe), surface.Probe);
        }
    }

    [Fact]
    public async Task Buffered_Input_Spills_Above_The_Configured_Threshold()
    {
        var response = await Client.PostBodyAsync(
            ProbePaths.Body + "?mode=spill",
            Encoding.UTF8.GetBytes(new string('s', 2048)));

        response.StatusCode.ShouldBe(200);
        response.Header(ProbeHeaders.Spilled).ShouldBe("True");
        response.Text.ShouldBe(Describe(new string('s', 2048)));
    }

    [Fact]
    public async Task SystemWeb_Rejects_A_Body_Above_MaxRequestLength()
    {
        var response = await Client.PostBodyAsync(
            ProbePaths.Body + "?mode=input",
            Enumerable.Repeat((byte)'l', 5000).ToArray());

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("Maximum request length exceeded", Case.Sensitive);
    }

    [Fact]
    public async Task SystemWeb_Rejects_An_Unknown_Length_Body_Above_MaxRequestLength()
    {
        var response = await Client.PostBodyAsync(
            ProbePaths.Body + "?mode=input",
            Enumerable.Repeat((byte)'c', 5000).ToArray(),
            BodyFraming.DelayedChunked);

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("Maximum request length exceeded", Case.Sensitive);
    }

    // Its own fixture: asyncPreloadMode="All" is a different application.
    [Fact]
    public async Task Async_Preload_Buffers_A_Delayed_Chunked_Body_Before_The_Handler()
    {
        using var run = LiveScenario.StartIsolated(
            Fixtures.BodyPreload, IsolationReason.ColdActivation);

        var response = await run.Client.PostBodyAsync(
            ProbePaths.Body + "?mode=preload",
            Encoding.UTF8.GetBytes("body:preload-delayed"),
            BodyFraming.DelayedChunked);

        response.StatusCode.ShouldBe(200);
        response.Header(ProbeHeaders.ReadMode).ShouldBe("Buffered");
        response.Text.ShouldBe(Describe("body:preload-delayed"));
    }

    // A dedicated single-connection client: the claim is that both requests crossed one
    // connection, which the shared client's pool cannot promise.
    [Fact]
    public async Task Kestrel_Drains_An_Unread_Body_Before_The_Next_Request_On_The_Connection()
    {
        using var client = new ScenarioClient(scenario.Address, maxConnectionsPerServer: 1);

        var unread = await client.PostBodyAsync(
            ProbePaths.Body + "?mode=unread",
            Encoding.UTF8.GetBytes("body:unread"));
        var followUp = await client.PostBodyAsync(
            ProbePaths.Body + "?mode=input",
            Encoding.UTF8.GetBytes("body:fixed-input"));

        unread.Text.ShouldBe("unread");
        followUp.Text.ShouldBe(Describe("body:fixed-input"));
        unread.Header(ProbeHeaders.RemotePort).ShouldNotBeNull();
        followUp.Header(ProbeHeaders.RemotePort).ShouldBe(unread.Header(ProbeHeaders.RemotePort));
    }

    [Fact]
    public async Task A_Client_Abort_During_A_Synchronous_Bufferless_Read_Becomes_HttpException()
    {
        var interim = await RawSocketProbe.AbortMidBodyAsync(aborts.Address, ProbePaths.Body + "?mode=abort");
        var detection = Stopwatch.StartNew();

        interim.ShouldBe(100);
        var outcome = await aborts.Witness.WaitForAsync(WitnessProtocol.BodyAbort, AbortDetectionBudget);

        // Recorded, never asserted: detection latency is load-dependent.
        output.WriteLine("abort detected after " + detection.ElapsedMilliseconds + "ms");
        outcome.ShouldBe("body-abort:System.Web.HttpException");
    }

    [Fact]
    public async Task A_Client_Abort_During_A_Worker_Apm_Read_Becomes_HttpException()
    {
        var interim = await RawSocketProbe.AbortMidBodyAsync(
            aborts.Address,
            ProbePaths.Body + "?mode=abort-apm");
        var detection = Stopwatch.StartNew();

        interim.ShouldBe(100);
        var outcome = await aborts.Witness.WaitForAsync("body-apm-abort:", AbortDetectionBudget);

        output.WriteLine("apm abort detected after " + detection.ElapsedMilliseconds + "ms");
        outcome.ShouldBe("body-apm-abort:System.Web.HttpException");
    }

    // The application's maxAllowedContentLength is the declared-length limit the host refuses
    // before the pipeline; a consumer's own Kestrel limit is left where Kestrel enforces it, on
    // the read. Its own host, since that limit is process-wide.
    [Fact]
    public async Task A_Declared_Length_Over_An_Explicit_Kestrel_Limit_Fails_The_Handler_Mid_Read()
    {
        using var run = LiveScenario.StartIsolated(
            Fixtures.Body, IsolationReason.HostConfiguration, kestrelMaxBody: 1024);

        var response = await run.Client.PostBodyAsync(
            ProbePaths.Body + "?mode=input",
            Enumerable.Repeat((byte)'k', 2048).ToArray());

        response.StatusCode.ShouldBe(413);
        (await run.Witness.HandlerEntriesAsync()).ShouldContain("handler-entered:input");
    }

    [Fact]
    public async Task An_Undeclared_Length_Over_The_Host_Limit_Fails_The_Handler_Mid_Read()
    {
        using var run = LiveScenario.StartIsolated(
            Fixtures.Body, IsolationReason.HostConfiguration, kestrelMaxBody: 1024);

        var response = await run.Client.PostBodyAsync(
            ProbePaths.Body + "?mode=input",
            Enumerable.Repeat((byte)'K', 2048).ToArray(),
            BodyFraming.Chunked);

        response.StatusCode.ShouldBe(413);
        (await run.Witness.HandlerEntriesAsync()).ShouldContain("handler-entered:input");
    }

    // An ordinary HttpException by the time System.Web sees it, so the app's error config wins.
    [Fact]
    public async Task Custom_Errors_Convert_A_Host_Rejection_Into_The_Application_Response()
    {
        using var run = LiveScenario.StartIsolated(
            Fixtures.BodyCustomErrors, IsolationReason.HostConfiguration, kestrelMaxBody: 1024);

        var response = await run.Client.PostBodyAsync(
            ProbePaths.Body + "?mode=input",
            Enumerable.Repeat((byte)'C', 2048).ToArray(),
            BodyFraming.Chunked);

        response.StatusCode.ShouldBe(302);
        (await run.Witness.HandlerEntriesAsync()).ShouldContain("handler-entered:input");
    }

    // IV14: integrated answered the oversize body with a keep-alive 500 carrying Content-Length,
    // where classic sent Connection: close, omitted the length and dropped the socket. Pipelined
    // on one raw connection, so the second response arrives only if the first left it open.
    [Theory]
    [InlineData("input")]
    [InlineData("bufferless")]
    public async Task An_Oversize_Body_Is_Refused_Without_Closing_The_Connection(string mode)
    {
        var oversize = new string('o', 5000);
        var request = $"""
            POST /body?mode={mode} HTTP/1.1
            Host: {scenario.Address.Authority}
            Content-Type: text/plain
            Content-Length: {oversize.Length}

            {oversize}GET /body?mode=input HTTP/1.1
            Host: {scenario.Address.Authority}
            Content-Length: 0
            Connection: close


            """.ReplaceLineEndings("\r\n");

        var wire = Encoding.ASCII.GetString(
            await RawSocketProbe.SendRawUntilQuietAsync(
                scenario.Address, Encoding.ASCII.GetBytes(request)));

        var refusal = wire[..wire.IndexOf("\r\n\r\n", StringComparison.Ordinal)];
        refusal.ShouldStartWith("HTTP/1.1 500 ", Case.Sensitive);
        refusal.ShouldContain("Content-Length: ", Case.Sensitive);
        refusal.ShouldNotContain("Connection: close");
        wire.ShouldContain("Maximum request length exceeded", Case.Sensitive);
        wire.Split("HTTP/1.1 ").Length.ShouldBe(3, wire);
    }

    private static string Describe(string value)
    {
        var body = Encoding.UTF8.GetBytes(value);
        return body.Length + ":" + Convert.ToHexString(SHA256.HashData(body));
    }
}
