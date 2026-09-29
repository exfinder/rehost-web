using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

public sealed class ScriptResourceOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    private async Task<string> FirstScriptUrlAsync()
    {
        var page = await scenario.Client.GetAsync("/ScriptResourceUrl.aspx");
        page.StatusCode.ShouldBe(200);

        var url = Regex.Match(page.Text, @"src=""(/ScriptResource\.axd\?[^""]+)""").Groups[1].Value;
        url.ShouldNotBeEmpty();

        return url.Replace("&amp;", "&");
    }

    private async Task<string> FirstScriptAsync()
    {
        var response = await scenario.Client.GetAsync(await FirstScriptUrlAsync());

        response.StatusCode.ShouldBe(200);
        return response.Text;
    }

    [Fact]
    public async Task A_Named_Script_Is_Served_From_The_Assembly()
    {
        var script = await FirstScriptAsync();

        script.ShouldStartWith("//----");
        script.ShouldContain("Sys.Debug.isDebug=false", Case.Sensitive);
    }

    [Fact]
    public async Task A_Head_Request_Reaches_The_Handler()
    {
        var response = await scenario.Client.HeadAsync(await FirstScriptUrlAsync());

        response.StatusCode.ShouldBe(200);
        response.Bytes.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Handler_Appends_The_Client_String_Resources()
    {
        // Sys.Res exists on no other delivery path: the script resource never carries it.
        var script = await FirstScriptAsync();

        script.ShouldContain("Type.registerNamespace('Sys');", Case.Sensitive);
        script.ShouldContain("Sys.Res={", Case.Sensitive);
        script.ShouldContain("\"argumentNull\":", Case.Sensitive);
    }
}
