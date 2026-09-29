using Rehost.Web.ScenarioProtocol;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// The body suite's request surfaces over h2c: HTTP/2 framing on the wire, System.Web's own
// HTTP/1.1-shaped view inside. Kestrel translates the framing; what this pins is that nothing in
// the adapter or the imported response path keys on the version string in a way h2 breaks.
public sealed class Http2OverKestrelTests : IDisposable
{
    private readonly LiveScenario _scenario = LiveScenario.StartIsolated(
        Fixtures.Body, IsolationReason.HostConfiguration, http2: true);

    [Fact]
    public async Task Fixed_And_Chunked_Bodies_Reach_The_Raw_Request_Surfaces_Over_Http2()
    {
        foreach (var (probe, mode, framing) in new[]
                 {
                     ("fixed-input", "input", BodyFraming.Fixed),
                     ("fixed-bufferless", "bufferless", BodyFraming.Fixed),
                     ("chunked-bufferless", "bufferless", BodyFraming.DelayedChunked),
                     ("chunked-apm", "apm", BodyFraming.DelayedChunked),
                 })
        {
            var response = await _scenario.Client.PostBodyAsync(
                ProbePaths.Body + "?mode=" + mode, Encoding.UTF8.GetBytes("body:" + probe), framing);

            response.Version.ShouldBe(HttpVersion.Version20, probe);
            response.StatusCode.ShouldBe(200, probe);
            response.Text.ShouldBe(Describe("body:" + probe), probe);
        }
    }

    [Fact]
    public async Task A_Spilled_Upload_Round_Trips_Over_Http2()
    {
        var response = await _scenario.Client.PostBodyAsync(
            ProbePaths.Body + "?mode=spill", Encoding.UTF8.GetBytes(new string('s', 2048)));

        response.Version.ShouldBe(HttpVersion.Version20);
        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe(Describe(new string('s', 2048)));
    }

    public void Dispose() => _scenario.Dispose();

    private static string Describe(string value)
    {
        var body = Encoding.UTF8.GetBytes(value);
        return body.Length + ":" + Convert.ToHexString(SHA256.HashData(body));
    }
}
