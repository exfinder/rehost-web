using System.Text;
using Shouldly;
using Rehost.WebForms.Parity.Contracts;
using Xunit;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Hosting.Tests;

// This application spills content above one kilobyte to a temporary file, so saving here streams
// out of that file rather than out of the byte array the postback fixture keeps in memory.
public sealed class RawRequestSaveOverKestrelTests(BodyLiveScenario scenario)
    : IClassFixture<BodyLiveScenario>
{
    private static readonly byte[] Spilled = Encoding.UTF8.GetBytes(new string('u', 2048));

    [Fact]
    public async Task Saves_A_Spilled_Upload_From_The_Temporary_File_Holding_It()
    {
        using var directory = new TempDirectory();
        var target = directory.Path("spilled.bin");

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var response = await scenario.Client.PostAsync(
            "/save?mode=file&to=" + Uri.EscapeDataString(target),
            PostbackForm.EncodeMultipart(
                fields,
                [new MultipartFile("Picked", "spilled.bin", "application/octet-stream", Spilled)]),
            "multipart/form-data; boundary=" + PostbackForm.MultipartBoundary);

        response.Text.ShouldBe("saved");
        // Without this the test would pass just as well from memory, proving nothing about the
        // file-backed branch it exists for.
        response.Header(ProbeHeaders.Spilled).ShouldBe("True");
        File.ReadAllBytes(target).ShouldBe(Spilled);
    }

    [Fact]
    public async Task Saves_The_Whole_Raw_Request()
    {
        using var directory = new TempDirectory();
        var target = directory.Path("request.bin");
        var body = Encoding.UTF8.GetBytes("a direct binary upload");

        var response = await scenario.Client.PostAsync(
            "/save?mode=raw&to=" + Uri.EscapeDataString(target),
            body,
            "application/octet-stream");

        response.Text.ShouldBe("saved");
        File.ReadAllBytes(target).ShouldBe(body);
    }

    [Fact]
    public async Task Saves_The_Raw_Request_Behind_Its_Request_Line_And_Headers()
    {
        using var directory = new TempDirectory();
        var target = directory.Path("request.txt");
        var body = Encoding.UTF8.GetBytes("a direct binary upload");

        var response = await scenario.Client.PostAsync(
            "/save?mode=raw-headers&to=" + Uri.EscapeDataString(target),
            body,
            "application/octet-stream");

        response.Text.ShouldBe("saved");

        var saved = File.ReadAllBytes(target);
        var separator = Encoding.ASCII.GetBytes("\r\n\r\n");
        var split = IndexOf(saved, separator);
        split.ShouldBeGreaterThan(0);

        var head = Encoding.UTF8.GetString(saved, 0, split);
        head.ShouldStartWith("POST /save?mode=raw-headers&to=");
        head.ShouldContain(" HTTP/1.1\r\n");
        head.ShouldContain("Content-Length: " + body.Length + "\r\n");
        head.ShouldContain("Content-Type: application/octet-stream\r\n");
        saved[(split + separator.Length)..].ShouldBe(body);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var start = 0; start + needle.Length <= haystack.Length; start++)
        {
            if (haystack.AsSpan(start, needle.Length).SequenceEqual(needle))
            {
                return start;
            }
        }

        return -1;
    }
}
