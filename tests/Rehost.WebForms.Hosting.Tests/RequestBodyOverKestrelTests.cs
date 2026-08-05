using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

[Collection(nameof(BodyCollection))]
public sealed class RequestBodyOverKestrelTests(
    BodyLiveScenario scenario,
    AbortLiveScenario aborts,
    ITestOutputHelper output)
{
    private static readonly TimeSpan AbortDetectionBudget = TimeSpan.FromSeconds(60);

    private ScenarioClient Client => scenario.Client;

    // The interim 100 of the expect-continue handshake is asserted explicitly by the abort tests,
    // whose raw socket observes it; here HttpClient drives the handshake and a described body
    // proves the upload crossed it.
    [Fact]
    public async Task Fixed_And_Chunked_Bodies_Reach_All_Raw_Request_Surfaces()
    {
        var surfaces = new (string Probe, string Mode, bool Chunked, bool ExpectContinue)[]
        {
            ("fixed-input", "input", false, false),
            ("fixed-binary", "binary", false, false),
            ("fixed-buffered", "buffered", false, false),
            ("fixed-bufferless", "bufferless", false, false),
            ("chunked-bufferless", "bufferless", true, false),
            ("chunked-apm", "apm", true, false),
            ("expect-continue", "bufferless", false, true),
        };

        foreach (var surface in surfaces)
        {
            var response = await Client.PostBodyAsync(
                "/body?mode=" + surface.Mode,
                Encoding.UTF8.GetBytes("body:" + surface.Probe),
                chunked: surface.Chunked,
                expectContinue: surface.ExpectContinue);

            response.StatusCode.ShouldBe(200, surface.Probe);
            response.Text.ShouldBe(Describe("body:" + surface.Probe), surface.Probe);
        }
    }

    [Fact]
    public async Task Buffered_Input_Spills_Above_The_Configured_Threshold()
    {
        var response = await Client.PostBodyAsync(
            "/body?mode=spill",
            Encoding.UTF8.GetBytes(new string('s', 2048)));

        response.StatusCode.ShouldBe(200);
        response.Header("X-Spilled").ShouldBe("True");
        response.Text.ShouldBe(Describe(new string('s', 2048)));
    }

    [Fact]
    public async Task SystemWeb_Rejects_A_Body_Above_MaxRequestLength()
    {
        var response = await Client.PostBodyAsync(
            "/body?mode=input",
            Enumerable.Repeat((byte)'l', 5000).ToArray());

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("Maximum request length exceeded");
    }

    [Fact]
    public async Task SystemWeb_Rejects_An_Unknown_Length_Body_Above_MaxRequestLength()
    {
        var response = await Client.PostBodyAsync(
            "/body?mode=input",
            Enumerable.Repeat((byte)'c', 5000).ToArray(),
            chunked: true);

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("Maximum request length exceeded");
    }

    // Its own fixture: asyncPreloadMode="All" is a different application.
    [Fact]
    public async Task Async_Preload_Buffers_A_Delayed_Chunked_Body_Before_The_Handler()
    {
        using var run = new LiveScenario(Fixtures.BodyPreload);

        var response = await run.Client.PostBodyAsync(
            "/body?mode=preload",
            Encoding.UTF8.GetBytes("body:preload-delayed"),
            chunked: true);

        response.StatusCode.ShouldBe(200);
        response.Header("X-Read-Mode").ShouldBe("Buffered");
        response.Text.ShouldBe(Describe("body:preload-delayed"));
    }

    // A dedicated single-connection client: the claim is that both requests crossed one
    // connection, which the shared client's pool cannot promise.
    [Fact]
    public async Task Kestrel_Drains_An_Unread_Body_Before_The_Next_Request_On_The_Connection()
    {
        using var client = new ScenarioClient(scenario.Address, maxConnectionsPerServer: 1);

        var unread = await client.PostBodyAsync(
            "/body?mode=unread",
            Encoding.UTF8.GetBytes("body:unread"));
        var followUp = await client.PostBodyAsync(
            "/body?mode=input",
            Encoding.UTF8.GetBytes("body:fixed-input"));

        unread.Text.ShouldBe("unread");
        followUp.Text.ShouldBe(Describe("body:fixed-input"));
        unread.Header("X-Remote-Port").ShouldNotBeNull();
        followUp.Header("X-Remote-Port").ShouldBe(unread.Header("X-Remote-Port"));
    }

    [Fact]
    public async Task A_Client_Abort_During_A_Synchronous_Bufferless_Read_Becomes_HttpException()
    {
        var interim = await RawSocketProbe.AbortMidBodyAsync(aborts.Address, "/body?mode=abort");
        var detection = Stopwatch.StartNew();

        interim.ShouldBe(100);
        var outcome = await aborts.Witness.WaitForAsync("body-abort:", AbortDetectionBudget);

        // Recorded, never asserted: detection latency is load-dependent.
        output.WriteLine("abort detected after " + detection.ElapsedMilliseconds + "ms");
        outcome.ShouldBe("body-abort:System.Web.HttpException");
    }

    [Fact]
    public async Task A_Client_Abort_During_A_Worker_Apm_Read_Becomes_HttpException()
    {
        var interim = await RawSocketProbe.AbortMidBodyAsync(
            aborts.Address,
            "/body?mode=abort-apm");
        var detection = Stopwatch.StartNew();

        interim.ShouldBe(100);
        var outcome = await aborts.Witness.WaitForAsync("body-apm-abort:", AbortDetectionBudget);

        output.WriteLine("apm abort detected after " + detection.ElapsedMilliseconds + "ms");
        outcome.ShouldBe("body-apm-abort:System.Web.HttpException");
    }

    // Its own host for two reasons. The Kestrel limit is process-wide, so sharing it with an
    // ordinary probe would reject that probe's body too; and the claim below is a negative over
    // every handler entry, which the chunked case would satisfy, so the two cannot share either.
    [Fact]
    public async Task Kestrel_Refuses_A_Declared_Length_Over_Its_Own_Limit_Before_The_Pipeline()
    {
        using var run = new LiveScenario(Fixtures.Body, "--kestrel-max-body", "1024");

        var response = await run.Client.PostBodyAsync(
            "/body?mode=input",
            Enumerable.Repeat((byte)'k', 2048).ToArray());

        response.StatusCode.ShouldBe(413);
        // Empty content type: System.Web never ran. An app-rendered rejection carries one.
        response.ContentType.ShouldBeNull();
        (await run.Witness.HandlerEntriesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_Undeclared_Length_Over_The_Host_Limit_Fails_The_Handler_Mid_Read()
    {
        using var run = new LiveScenario(Fixtures.Body, "--kestrel-max-body", "1024");

        var response = await run.Client.PostBodyAsync(
            "/body?mode=input",
            Enumerable.Repeat((byte)'K', 2048).ToArray(),
            chunked: true);

        response.StatusCode.ShouldBe(413);
        (await run.Witness.HandlerEntriesAsync()).ShouldContain("handler-entered:input");
    }

    // An ordinary HttpException by the time System.Web sees it, so the app's error config wins.
    [Fact]
    public async Task Custom_Errors_Convert_A_Host_Rejection_Into_The_Application_Response()
    {
        using var run = new LiveScenario(Fixtures.BodyCustomErrors, "--kestrel-max-body", "1024");

        var response = await run.Client.PostBodyAsync(
            "/body?mode=input",
            Enumerable.Repeat((byte)'C', 2048).ToArray(),
            chunked: true);

        response.StatusCode.ShouldBe(302);
        (await run.Witness.HandlerEntriesAsync()).ShouldContain("handler-entered:input");
    }

    private static string Describe(string value)
    {
        var body = Encoding.UTF8.GetBytes(value);
        return body.Length + ":" + Convert.ToHexString(SHA256.HashData(body));
    }
}
