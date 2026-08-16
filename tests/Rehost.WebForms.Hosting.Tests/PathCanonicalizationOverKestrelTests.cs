using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The wiring of the P72 pieces on a live host — the adapter's canonical path/RawUrl, the
// handler-mapping path-info split, and the middleware's above-root refusal — each of which is
// unit-tested on its own; the rule tables live there. Expected values are the IIS reading.
public sealed class PathCanonicalizationOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    // System.Uri would resolve dot segments client-side, so the target goes on the wire as
    // written.
    private async Task<(int Status, string Body)> Raw(string target)
    {
        var raw = System.Text.Encoding.UTF8.GetString(
            await RawSocketProbe.GetRawResponseAsync(scenario.Address, target));
        var status = int.Parse(raw.Substring(9, 3));
        var body = raw.Substring(raw.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4);
        return (status, body);
    }

    private async Task<Dictionary<string, string>> Probe(string target)
    {
        var (status, body) = await Raw(target);
        status.ShouldBe(200, body);
        return body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1]);
    }

    [Fact]
    public async Task A_Handler_Url_With_Path_Info_Runs_The_Handler_And_Splits_The_Path()
    {
        var probe = await Probe("/pi/echo.probe/extra/info.txt");

        probe["filepath"].ShouldBe("/pi/echo.probe");
        probe["pathinfo"].ShouldBe("/extra/info.txt");
        probe["path"].ShouldBe("/pi/echo.probe/extra/info.txt");
        probe["physical"].ShouldBe(
            Path.Combine(scenario.ApplicationPath, "pi", "echo.probe"));
    }

    [Fact]
    public async Task Raw_Url_Is_Canonical_And_Decoded_With_The_Query_Verbatim()
    {
        var probe = await Probe("/pi/./sub%2F..%2Fecho.probe?a=%2F..%2Fx");

        probe["rawurl"].ShouldBe("/pi/echo.probe?a=%2F..%2Fx");
        probe["path"].ShouldBe("/pi/echo.probe");
    }

    [Fact]
    public async Task Path_Info_Does_Not_Trip_The_Hidden_Segment_Rule()
    {
        var probe = await Probe("/pi/echo.probe/bin/x");

        probe["pathinfo"].ShouldBe("/bin/x");
    }

    [Fact]
    public async Task A_Climb_Above_The_Root_Is_Refused_At_The_Front_Door()
    {
        var (status, body) = await Raw("/../pi/x.txt");

        status.ShouldBe(403);
        body.ShouldNotContain("pi-static");
    }

    [Fact]
    public async Task An_Encoded_Slash_Serves_A_Static_File()
    {
        var response = await scenario.Client.GetAsync("/pi%2Fx.txt");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("pi-static");
    }
}
