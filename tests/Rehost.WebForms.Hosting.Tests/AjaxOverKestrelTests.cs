using System.Text;

using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// First reach of the partial-rendering pipeline over the port: PageRequestManager's delta
// response through HttpResponse.SwitchWriter, ScriptModule's error/redirect interception and
// page-method routing, Timer-driven async posts, and the QueryExtender expression pipeline.
public sealed class AjaxOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
        + "Chrome/109.0.0.0 Safari/537.36";

    private Task<ScenarioResponse> GetAsBrowserAsync(string path) =>
        scenario.Client.GetWithHeadersAsync(path, ("User-Agent", BrowserUserAgent));

    private async Task<ScenarioResponse> AsyncPostAsync(
        string path, string panelsAndTarget, params (string Name, string Value)[] overrides)
    {
        var page = await GetAsBrowserAsync(path);
        page.StatusCode.ShouldBe(200, page.Text);
        return await AsyncPostFromAsync(page.Text, path, panelsAndTarget, overrides);
    }

    private async Task<ScenarioResponse> AsyncPostFromAsync(
        string html, string path, string panelsAndTarget, params (string Name, string Value)[] overrides)
    {
        var fields = PostbackForm.Fields(html);
        fields["SM"] = panelsAndTarget;
        fields["__ASYNCPOST"] = "true";
        foreach (var (name, value) in overrides)
        {
            fields[name] = value;
        }

        return await scenario.Client.PostWithHeadersAsync(
            path,
            Encoding.UTF8.GetBytes(PostbackForm.Encode(fields)),
            "application/x-www-form-urlencoded; charset=utf-8",
            ("X-MicrosoftAjax", "Delta=true"),
            ("User-Agent", BrowserUserAgent));
    }

    // len|type|id|content| — len counts content characters, so content may itself contain '|'.
    private static List<(string Type, string Id, string Content)> ParseDelta(string text)
    {
        var segments = new List<(string, string, string)>();
        var position = 0;
        while (position < text.Length)
        {
            var lengthEnd = text.IndexOf('|', position);
            var length = int.Parse(text[position..lengthEnd]);
            var typeEnd = text.IndexOf('|', lengthEnd + 1);
            var type = text[(lengthEnd + 1)..typeEnd];
            var idEnd = text.IndexOf('|', typeEnd + 1);
            var id = text[(typeEnd + 1)..idEnd];
            var content = text.Substring(idEnd + 1, length);
            segments.Add((type, id, content));
            position = idEnd + 1 + length + 1;
        }

        return segments;
    }

    [Fact]
    public async Task Get_Renders_The_Panel_Scaffolding()
    {
        var response = await GetAsBrowserAsync("/ajax/Panel.aspx");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldContain("""<div id="Panel">""");
        response.Text.ShouldContain("inside:initial");
        response.Text.ShouldContain("Sys.WebForms.PageRequestManager._initialize", Case.Sensitive);
        response.Text.ShouldContain("ScriptResource.axd", Case.Sensitive);
    }

    [Fact]
    public async Task Async_Post_Answers_A_Delta_That_Updates_Only_The_Panel()
    {
        var response = await AsyncPostAsync("/ajax/Panel.aspx", "Panel|Bump", ("Bump", "Bump"));

        response.StatusCode.ShouldBe(200, response.Text);
        response.Headers["Content-Type"].ShouldStartWith("text/plain");
        var segments = ParseDelta(response.Text);
        var panel = segments.Single(s => s.Type == "updatePanel" && s.Id == "Panel");
        panel.Content.ShouldContain("inside:bumped");
        response.Text.ShouldNotContain("outside:bumped");
        segments.ShouldContain(s => s.Type == "hiddenField" && s.Id == "__VIEWSTATE");
        segments.ShouldContain(s => s.Type == "asyncPostBackControlIDs");
    }

    [Fact]
    public async Task Registered_Outside_Trigger_Refreshes_The_Panel()
    {
        var response = await AsyncPostAsync(
            "/ajax/Panel.aspx", "Panel|Refresh", ("Refresh", "Refresh"));

        response.StatusCode.ShouldBe(200, response.Text);
        var segments = ParseDelta(response.Text);
        segments.Single(s => s.Type == "updatePanel" && s.Id == "Panel")
            .Content.ShouldContain("inside:refreshed");
    }

    [Fact]
    public async Task Delta_View_State_Round_Trips_Into_The_Next_Async_Post()
    {
        var page = await GetAsBrowserAsync("/ajax/Panel.aspx");
        var first = await AsyncPostFromAsync(page.Text, "/ajax/Panel.aspx", "Panel|Bump", ("Bump", "Bump"));
        var carried = ParseDelta(first.Text)
            .Where(s => s.Type == "hiddenField")
            .ToDictionary(s => s.Id, s => s.Content);

        var fields = PostbackForm.Fields(page.Text);
        foreach (var entry in carried)
        {
            fields[entry.Key] = entry.Value;
        }

        fields["SM"] = "Panel|Refresh";
        fields["__ASYNCPOST"] = "true";
        fields["Refresh"] = "Refresh";
        var second = await scenario.Client.PostWithHeadersAsync(
            "/ajax/Panel.aspx",
            Encoding.UTF8.GetBytes(PostbackForm.Encode(fields)),
            "application/x-www-form-urlencoded; charset=utf-8",
            ("X-MicrosoftAjax", "Delta=true"),
            ("User-Agent", BrowserUserAgent));

        second.StatusCode.ShouldBe(200, second.Text);
        ParseDelta(second.Text)
            .Single(s => s.Type == "updatePanel" && s.Id == "Panel")
            .Content.ShouldContain("inside:refreshed");
    }

    [Fact]
    public async Task Async_Error_Rides_The_Error_Token_On_A_200()
    {
        var response = await AsyncPostAsync("/ajax/Panel.aspx", "Panel|Fail", ("Fail", "Fail"));

        response.StatusCode.ShouldBe(200, response.Text);
        response.Headers["Content-Type"].ShouldStartWith("text/plain");
        var error = ParseDelta(response.Text).Single(s => s.Type == "error");
        error.Id.ShouldBe("500");
        error.Content.ShouldContain("panel-deliberate-failure");
    }

    [Fact]
    public async Task AsyncPostBackErrorMessage_Replaces_The_Exception_Text()
    {
        var response = await AsyncPostAsync(
            "/ajax/Panel.aspx", "Panel|Fail", ("Fail", "Fail"), ("Friendly", "yes"));

        response.StatusCode.ShouldBe(200, response.Text);
        var error = ParseDelta(response.Text).Single(s => s.Type == "error");
        error.Content.ShouldBe("panel-friendly-message");
        response.Text.ShouldNotContain("panel-deliberate-failure");
    }

    [Fact]
    public async Task Redirect_During_Async_Post_Becomes_A_PageRedirect_Token()
    {
        var response = await AsyncPostAsync("/ajax/Panel.aspx", "Panel|Go", ("Go", "Go"));

        response.StatusCode.ShouldBe(200, response.Text);
        ParseDelta(response.Text).Single(s => s.Type == "pageRedirect")
            .Content.ShouldBe("%2fajax%2fPanel.aspx%3ffrom%3dredirect");
    }

    [Fact]
    public async Task Timer_Tick_Drives_The_Panel_Update()
    {
        var response = await AsyncPostAsync("/ajax/Panel.aspx", "Panel|T", ("__EVENTTARGET", "T"));

        response.StatusCode.ShouldBe(200, response.Text);
        ParseDelta(response.Text)
            .Single(s => s.Type == "updatePanel" && s.Id == "Panel")
            .Content.ShouldContain("clock:ticked");
    }

    [Fact]
    public async Task Page_Renders_The_PageMethods_Proxy()
    {
        var response = await GetAsBrowserAsync("/ajax/Panel.aspx");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldContain("PageMethods", Case.Sensitive);
        response.Text.ShouldContain("Echo", Case.Sensitive);
    }

    [Fact]
    public async Task Page_Method_Post_Answers_In_The_D_Wrapper()
    {
        var response = await scenario.Client.PostWithHeadersAsync(
            "/ajax/Panel.aspx/Echo",
            Encoding.UTF8.GetBytes("{\"text\":\"hello\"}"),
            "application/json; charset=utf-8");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Headers["Content-Type"].ShouldBe("application/json; charset=utf-8");
        response.Text.ShouldBe("{\"d\":\"pm:hello\"}");
    }

    [Fact]
    public async Task Page_Method_Path_Without_Json_Renders_The_Page()
    {
        var response = await scenario.Client.GetAsync("/ajax/Panel.aspx/Echo");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldContain("<html");
        response.Text.ShouldNotContain("\"d\"");
    }

    [Fact]
    public async Task Builtin_Authentication_AppService_Maps_And_Reports_Disabled()
    {
        var response = await scenario.Client.PostWithHeadersAsync(
            "/Authentication_JSON_AppService.axd/IsLoggedIn",
            Encoding.UTF8.GetBytes("{}"),
            "application/json; charset=utf-8");

        response.StatusCode.ShouldBe(500);
        response.Headers["Content-Type"].ShouldStartWith("application/json");
        response.Text.ShouldContain("AuthenticationService is disabled.", Case.Sensitive);
    }

    [Fact]
    public async Task Builtin_Role_AppService_Maps_And_Reports_Disabled()
    {
        var response = await scenario.Client.PostWithHeadersAsync(
            "/Role_JSON_AppService.axd/GetRolesForCurrentUser",
            Encoding.UTF8.GetBytes("{}"),
            "application/json; charset=utf-8");

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("RoleService is disabled.", Case.Sensitive);
    }

    [Fact]
    public async Task QueryExtender_Filters_And_Orders_The_Queryable_Source()
    {
        var response = await scenario.Client.GetAsync("/ajax/Query.aspx");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldContain("[Grace:85][Linus:55][Ada:36]", Case.Sensitive);
        response.Text.ShouldNotContain("Brendan");
    }
}
