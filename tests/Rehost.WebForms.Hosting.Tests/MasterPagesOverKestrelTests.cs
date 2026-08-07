using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Master pages and user controls ride untouched Reference Source, but their templates are the
// first reach of the .master/.ascx build providers through the port-owned compilation substrate,
// and the cached fragment is the first reach of PartialCachingControl over the port's cache.
// These pin the composed render, Framework's naming-container mangling, event routing through a
// master-hosted form, and fragment replay.
public sealed class MasterPagesOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task Composes_Content_Into_The_Master_Layout()
    {
        var response = await scenario.Client.GetAsync("/compose/Composed.aspx?q=42");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("<title>\r\n\tFrom content page\r\n</title>");
        response.Text.ShouldContain("<meta name=\"probe\" content=\"head-content\" />");
        response.Text.ShouldContain("<span id=\"ChromeLabel\">master-load-ran</span>");
        response.Text.ShouldContain("<span id=\"MainContent_PageLabel\">content-load-ran</span>");
        response.Text.ShouldContain("<div id=\"footer\">footer-from-master</div>");
        response.Text.ShouldNotContain("placeholder-default");

        var chrome = response.Text.IndexOf("id=\"chrome\"", StringComparison.Ordinal);
        var main = response.Text.IndexOf("id=\"main\"", StringComparison.Ordinal);
        var footer = response.Text.IndexOf("id=\"footer\"", StringComparison.Ordinal);
        chrome.ShouldBeLessThan(main);
        main.ShouldBeLessThan(footer);
    }

    [Fact]
    public async Task A_Registered_User_Control_Renders_Inside_Its_Naming_Container()
    {
        var response = await scenario.Client.GetAsync("/compose/Composed.aspx?q=42");

        response.Text.ShouldContain(
            "<span id=\"MainContent_TheWidget_WidgetLabel\">widget-load-ran:42</span>");
    }

    [Fact]
    public async Task Content_Flows_Through_Nested_Masters()
    {
        var response = await scenario.Client.GetAsync("/compose/NestedPage.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("<span id=\"ChromeLabel\">master-load-ran</span>");
        response.Text.ShouldContain("<div id=\"section\">section-chrome-from-nested-master</div>");
        response.Text.ShouldContain("<p id=\"leaf\">leaf-content-through-two-masters</p>");
    }

    [Fact]
    public async Task MasterType_Exposes_The_Master_As_Its_Generated_Type()
    {
        var response = await scenario.Client.GetAsync("/compose/Typed.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("<span id=\"ChromeLabel\">set-through-typed-master</span>");
    }

    [Fact]
    public async Task LoadControl_Adds_A_User_Control_At_Runtime()
    {
        var response = await scenario.Client.GetAsync("/compose/Dyn.aspx?q=dyn");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain(
            "<span id=\"MainContent_DynWidget_WidgetLabel\">widget-load-ran:dyn</span>");
    }

    // The field names are pinned literals: ctl00 is the master's generated ID, and losing that
    // mangling would break every client script and test recorded against Framework markup.
    [Fact]
    public async Task A_Button_In_A_Master_Hosted_Form_Raises_Its_Event()
    {
        var render = (await scenario.Client.GetAsync("/compose/MasterForm.aspx")).Text;
        render.ShouldContain("name=\"ctl00$MainContent$Entry\"");
        render.ShouldContain("<span id=\"MainContent_Echo\">not-clicked</span>");

        // The form action is relative to the page's directory, as a browser resolves it.
        var action = new Uri(
            new Uri(scenario.Address, "/compose/MasterForm.aspx"),
            PostbackForm.Action(render)).PathAndQuery;
        var response = await scenario.Client.PostFormAsync(
            action,
            PostbackForm.Body(render, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ctl00$MainContent$Entry"] = "typed by the client",
                ["ctl00$MainContent$Submit"] = "Go",
            }));

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain(
            "<span id=\"MainContent_Echo\">clicked-with:typed by the client</span>");
        response.Text.ShouldContain("value=\"typed by the client\"");
    }

    // The page stamps every render while the control stamps only when it really renders, so a
    // broken fragment cache changes the second widget stamp and a page cached whole repeats the
    // page stamp; either degradation fails exactly one of the two comparisons.
    [Fact]
    public async Task A_Cached_User_Control_Replays_Its_Fragment()
    {
        static (string Page, string Widget) Stamps(string html)
        {
            var page = System.Text.RegularExpressions.Regex.Match(
                html, "<p id=\"page-stamp\">(?<v>[0-9a-f]{32})</p>");
            var widget = System.Text.RegularExpressions.Regex.Match(
                html, "<span id=\"cached-stamp\">(?<v>[0-9a-f]{32})</span>");
            page.Success.ShouldBeTrue();
            widget.Success.ShouldBeTrue();
            return (page.Groups["v"].Value, widget.Groups["v"].Value);
        }

        var first = Stamps((await scenario.Client.GetAsync("/compose/Cached.aspx")).Text);
        var second = Stamps((await scenario.Client.GetAsync("/compose/Cached.aspx")).Text);

        second.Page.ShouldNotBe(first.Page);
        second.Widget.ShouldBe(first.Widget);
    }
}
