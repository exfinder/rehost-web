using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The wiring only a live host proves: the fixture's <clientCache> reaches the static bridge as
// one Cache-Control line, and the revalidation answer carries the same one. Read off the wire,
// since HttpClient parses Cache-Control and re-serializes its directives with a space.
public sealed class ClientCacheOverKestrelTests(CustomErrorsLiveScenario scenario)
    : IClassFixture<CustomErrorsLiveScenario>
{
    private const string Path = "/he/err404.json";

    [Fact]
    public async Task A_Static_File_And_Its_304_Carry_The_Configured_Cache_Control()
    {
        var response = await scenario.Client.GetAsync(Path);
        var lastModified = response.Header("Last-Modified");
        lastModified.ShouldNotBeNull();

        var served = Head(await RawSocketProbe.GetRawResponseAsync(scenario.Address, Path));
        var revalidation = Head(
            await RawSocketProbe.SendRawAsync(scenario.Address, Conditional(lastModified!)));

        response.StatusCode.ShouldBe(200);
        served.ShouldContain("Cache-Control: public,max-age=60\r\n", Case.Sensitive);
        served.Split("Cache-Control:").Length.ShouldBe(2, served);
        revalidation.ShouldStartWith("HTTP/1.1 304", Case.Sensitive);
        revalidation.ShouldContain("Cache-Control: public,max-age=60\r\n", Case.Sensitive);
    }

    private byte[] Conditional(string lastModified) => Encoding.ASCII.GetBytes(
        $"""
        GET {Path} HTTP/1.1
        Host: {scenario.Address.Authority}
        If-Modified-Since: {lastModified}
        Connection: close


        """.ReplaceLineEndings("\r\n"));

    private static string Head(byte[] raw) => RawResponse.Split(raw).Headers;
}
