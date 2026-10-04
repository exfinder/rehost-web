using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

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

    [Fact]
    public async Task A_Folder_Config_Replaces_The_Mode_And_Keeps_The_Root_Custom_Text()
    {
        var served = Head(
            await RawSocketProbe.GetRawResponseAsync(scenario.Address, "/sub/a.json"));

        served.ShouldStartWith("HTTP/1.1 200", Case.Sensitive);
        served.ShouldContain("Cache-Control: public,no-cache\r\n", Case.Sensitive);
        served.Split("Cache-Control:").Length.ShouldBe(2, served);
    }

    [Fact]
    public async Task A_Nested_Folder_Config_Merges_Over_Its_Parent_Folder()
    {
        var served = Head(
            await RawSocketProbe.GetRawResponseAsync(scenario.Address, "/sub/deep/a.json"));

        served.ShouldStartWith("HTTP/1.1 200", Case.Sensitive);
        served.ShouldContain("Cache-Control: public,max-age=7200\r\n", Case.Sensitive);
        served.Split("Cache-Control:").Length.ShouldBe(2, served);
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
