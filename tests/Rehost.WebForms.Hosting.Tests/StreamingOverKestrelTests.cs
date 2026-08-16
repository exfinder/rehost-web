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

    [Fact]
    public async Task The_First_Flush_Reaches_The_Client_Before_The_Handler_Continues()
    {
        var arrivals = await RawSocketProbe.ReadTimedAsync(
            scenario.Address, "/stream/flush?delay=" + (int)Delay.TotalMilliseconds);

        var first = Encoding.ASCII.GetString(arrivals[0].Bytes);
        arrivals[0].Elapsed.ShouldBeLessThan(Delay / 2, first);
        first.ShouldStartWith("HTTP/1.1 200 OK\r\n");
        first.ShouldContain("Transfer-Encoding: chunked\r\n");
        first.ShouldNotContain("Content-Length:");
        first.ShouldEndWith("\r\n\r\n6\r\npart1\n\r\n");

        var all = Encoding.ASCII.GetString(arrivals.SelectMany(arrival => arrival.Bytes).ToArray());
        var body = all[(all.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
        body.ShouldBe("6\r\npart1\n\r\n6\r\npart2\n\r\n6\r\npart3\n\r\n0\r\n\r\n");

        var part2 = arrivals.First(arrival => Encoding.ASCII.GetString(arrival.Bytes).Contains("part2"));
        part2.Elapsed.ShouldBeGreaterThanOrEqualTo(Delay - TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task Response_End_After_A_Flush_Terminates_The_Chunked_Body()
    {
        var arrivals = await RawSocketProbe.ReadTimedAsync(scenario.Address, "/stream/flush?case=end&delay=300");

        var all = Encoding.ASCII.GetString(arrivals.SelectMany(arrival => arrival.Bytes).ToArray());
        all.ShouldStartWith("HTTP/1.1 200 OK\r\n");
        all[(all.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]
            .ShouldBe("6\r\npart1\n\r\n6\r\npart2\n\r\n0\r\n\r\n");
    }

    // IIS coalesced the file and the write that followed it into one chunk (R-S3).
    [Fact]
    public async Task A_File_Sent_Between_Flushes_Streams_In_Order()
    {
        var arrivals = await RawSocketProbe.ReadTimedAsync(scenario.Address, "/stream/flush?case=file&delay=300");

        var all = Encoding.ASCII.GetString(arrivals.SelectMany(arrival => arrival.Bytes).ToArray());
        all[(all.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]
            .ShouldBe("6\r\npart1\n\r\n19\r\nFILEDATA-0123456789part2\n\r\n6\r\npart3\n\r\n0\r\n\r\n");
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

    [Fact]
    public async Task An_Async_Flush_Reaches_The_Client_Before_The_Handler_Continues()
    {
        var arrivals = await RawSocketProbe.ReadTimedAsync(
            scenario.Address, "/stream/flush?case=async&delay=" + (int)Delay.TotalMilliseconds);

        arrivals[0].Elapsed.ShouldBeLessThan(Delay / 2);
        Encoding.ASCII.GetString(arrivals[0].Bytes).ShouldEndWith("6\r\npart1\n\r\n");
        var all = Encoding.ASCII.GetString(arrivals.SelectMany(arrival => arrival.Bytes).ToArray());
        all[(all.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..]
            .ShouldBe("6\r\npart1\n\r\n6\r\npart2\n\r\n0\r\n\r\n");
    }
}
