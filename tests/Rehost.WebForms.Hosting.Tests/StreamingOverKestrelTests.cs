using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Response.Flush mid-request against the IIS + Framework readings (R-S1..R-S5,
// research/websockets-and-streaming.md): the first flush puts the head and the first chunk on the
// wire before the handler continues; System.Web frames the body itself (Transfer-Encoding:
// chunked, one chunk per flush); End after a flush terminates cleanly; a header change after a
// flush is System.Web's own refusal; an error after a flush keeps the 200 and appends the error.
public sealed class StreamingOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(1200);

    // Chunk boundaries are a transport detail — System.Web coalesces or splits a TransmitFile
    // and the write after it depending on when the buffering flag flips, and that timing differs
    // by platform. The portable contract is the decoded payload, that the encoding is chunked,
    // and the flush timing; this reads the chunked body back to bytes.
    private static (string Headers, string Body) SplitAndDechunk(byte[] raw)
    {
        var text = Encoding.ASCII.GetString(raw);
        var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headers = text[..headerEnd];
        var rest = text[(headerEnd + 4)..];
        var body = new StringBuilder();
        var i = 0;
        while (true)
        {
            var eol = rest.IndexOf("\r\n", i, StringComparison.Ordinal);
            var size = Convert.ToInt32(rest[i..eol], 16);
            if (size == 0)
            {
                break;
            }

            body.Append(rest, eol + 2, size);
            i = eol + 2 + size + 2;
        }

        return (headers, body.ToString());
    }

    [Fact]
    public async Task The_First_Flush_Reaches_The_Client_Before_The_Handler_Continues()
    {
        var arrivals = await RawSocketProbe.ReadTimedAsync(
            scenario.Address, "/stream/flush?delay=" + (int)Delay.TotalMilliseconds);

        // Everything the client held before the handler's first delay elapsed. Whether the head
        // and the first chunk land in one TCP segment or two is the transport's call, so this
        // asserts on the accumulated bytes, not on a single read.
        var beforeDelay = Encoding.ASCII.GetString(
            arrivals.TakeWhile(arrival => arrival.Elapsed < Delay / 2)
                .SelectMany(arrival => arrival.Bytes).ToArray());
        beforeDelay.ShouldStartWith("HTTP/1.1 200 OK\r\n");
        beforeDelay.ShouldContain("Transfer-Encoding: chunked\r\n");
        beforeDelay.ShouldNotContain("Content-Length:");
        beforeDelay.ShouldContain("6\r\npart1\n\r\n");
        beforeDelay.ShouldNotContain("part2");

        var (_, body) = SplitAndDechunk(arrivals.SelectMany(arrival => arrival.Bytes).ToArray());
        body.ShouldBe("part1\npart2\npart3\n");

        var part2 = arrivals.First(arrival => Encoding.ASCII.GetString(arrival.Bytes).Contains("part2"));
        part2.Elapsed.ShouldBeGreaterThanOrEqualTo(Delay - TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task Response_End_After_A_Flush_Terminates_The_Chunked_Body()
    {
        var arrivals = await RawSocketProbe.ReadTimedAsync(scenario.Address, "/stream/flush?case=end&delay=300");

        var (headers, body) = SplitAndDechunk(arrivals.SelectMany(arrival => arrival.Bytes).ToArray());
        headers.ShouldStartWith("HTTP/1.1 200 OK\r\n");
        body.ShouldBe("part1\npart2\n");
    }

    // IIS coalesced the file and the write that followed it into one chunk (R-S3).
    [Fact]
    public async Task A_File_Sent_Between_Flushes_Streams_In_Order()
    {
        var arrivals = await RawSocketProbe.ReadTimedAsync(scenario.Address, "/stream/flush?case=file&delay=300");

        var (_, body) = SplitAndDechunk(arrivals.SelectMany(arrival => arrival.Bytes).ToArray());
        body.ShouldBe("part1\nFILEDATA-0123456789part2\npart3\n");
    }

    [Fact]
    public async Task Head_Changes_After_A_Flush_Are_Refused_As_On_Framework()
    {
        var response = await scenario.Client.GetAsync("/stream/flush?case=lateheader");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe(
            "part1\n"
            + "append-ex:HttpException:Server cannot append header after HTTP headers have been sent.\n"
            + "status-ex:HttpException:Server cannot set status after HTTP headers have been sent.\n"
            + "cookie-ex:HttpException:Server cannot modify cookies after HTTP headers have been sent.\n"
            + "clientconnected=True\n");
        response.Header("X-Late").ShouldBeNull();
    }

    [Fact]
    public async Task An_Error_After_A_Flush_Keeps_The_Sent_Status_And_Appends_The_Error()
    {
        var response = await scenario.Client.GetAsync("/stream/flush?case=error");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldStartWith("part1\n");
        response.Text.ShouldContain("boom after flush");
    }

    // An asynchronous handler past its first await is off the cancellable period, so
    // Response.FlushAsync takes the worker request's BeginFlush/EndFlush arm rather than the
    // synchronous flush. The response is chunked and correct on every platform; whether the flush
    // reaches the wire before the handler continues is not guaranteed on this arm (Windows Kestrel
    // holds it), so unlike the synchronous flush this asserts the framing and payload, not timing.
    [Fact]
    public async Task An_Async_Handlers_FlushAsync_Produces_A_Chunked_Streamed_Response()
    {
        var arrivals = await RawSocketProbe.ReadTimedAsync(
            scenario.Address, "/stream/asyncflush?delay=200");

        var raw = arrivals.SelectMany(arrival => arrival.Bytes).ToArray();
        Encoding.ASCII.GetString(raw).ShouldContain("Transfer-Encoding: chunked\r\n");
        var (headers, body) = SplitAndDechunk(raw);
        headers.ShouldNotContain("Content-Length:");
        body.ShouldBe("part1\npart2\n");
    }
}
