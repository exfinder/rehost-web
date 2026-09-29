using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// The IIS URL Rewrite module's inbound rules over the fixture's <rewrite> section: every pattern
// sits under rw/ so no other class's request on this host meets one.
public sealed class RewriteOverKestrelTests(WebServerLiveScenario scenario)
    : IClassFixture<WebServerLiveScenario>
{
    [Fact]
    public async Task A_Rewritten_Page_Keeps_The_Original_Url_And_Moves_Every_Other_Member()
    {
        var (traced, stages) = await scenario.TracedGetAsync(this, "/rw/clean/5");
        var response = await scenario.Client.GetAsync("/rw/clean/5?extra=9");

        traced.StatusCode.ShouldBe(200);
        stages.ShouldContain("BeginRequest");
        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("path=/rw-probe.aspx\n", Case.Sensitive);
        response.Text.ShouldContain("rawurl=/rw/clean/5?extra=9\n", Case.Sensitive);
        response.Text.ShouldContain("query=id=5&extra=9\n", Case.Sensitive);
        response.Text.ShouldContain("xoriginal=/rw/clean/5?extra=9\n", Case.Sensitive);
        response.Text.ShouldContain("rewritten=1\n", Case.Sensitive);
        response.Text.ShouldContain("requesturi=/rw/clean/5?extra=9\n", Case.Sensitive);
        response.Text.ShouldContain("inallkeys=False,False,False,False\n", Case.Sensitive);
        response.Text.ShouldContain("action=\"./5?id=5&amp;extra=9\"", Case.Sensitive);
    }

    // The first pass judges the script file the handler map claims, not the whole URL: path info
    // after an existing page passes while a denied name in a URL no handler claims is refused
    // before any rule.
    [Fact]
    public async Task The_First_Pass_Judges_The_Script_File_Not_The_Path_Info()
    {
        var direct = await scenario.Client.GetAsync("/rw-probe.aspx/x.cs");
        var rewrittenInfo = await scenario.Client.GetAsync("/rw/pi/one");
        var deniedOriginal = await scenario.Client.GetAsync("/rw/pi/x.cs");

        direct.StatusCode.ShouldBe(200);
        direct.Text.ShouldContain("pathinfo=/x.cs\n", Case.Sensitive);
        rewrittenInfo.StatusCode.ShouldBe(200);
        rewrittenInfo.Text.ShouldContain("pathinfo=/one\n", Case.Sensitive);
        rewrittenInfo.Text.ShouldContain("rawurl=/rw/pi/one\n", Case.Sensitive);
        deniedOriginal.StatusCode.ShouldBe(404);
    }

    // The rewritten URL faces request filtering again, so a rule cannot reach hidden content; the
    // un-hidden sibling is the control that the rule itself works.
    [Fact]
    public async Task A_Rule_Cannot_Reach_A_Hidden_Segment()
    {
        Seed("Private", "secret.probe", "secret");
        Seed("App_Data", "open.probe", "open");

        var allowed = await scenario.Client.GetAsync("/rw/open");
        var hidden = await scenario.Client.GetAsync("/rw/hidden");

        allowed.StatusCode.ShouldBe(200);
        allowed.Text.ShouldBe("open");
        hidden.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task A_Redirect_Rule_Answers_Absolutely_Before_Any_Managed_Event()
    {
        var (response, stages) = await scenario.TracedGetAllowingSilenceAsync(this, "/rw/old");

        response.StatusCode.ShouldBe(301);
        response.Header("Location").ShouldBe(
            scenario.Address.GetLeftPart(UriPartial.Authority) + "/rw/new");
        response.Text.ShouldContain("Object Moved", Case.Sensitive);
        stages.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Rule_Onto_A_Folder_Serves_Its_Default_Document()
    {
        var response = await scenario.Client.GetAsync("/rw/dir");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("rw-dir-default\n");
    }

    [Fact]
    public async Task A_Denied_Original_Never_Reaches_The_Rules()
    {
        var (response, stages) = await scenario.TracedGetAllowingSilenceAsync(this, "/rw/bin/5");

        response.StatusCode.ShouldBe(404);
        stages.ShouldBeEmpty();
    }

    private void Seed(string folder, string name, string content)
    {
        var physical = Path.Combine(scenario.ApplicationPath, folder);
        Directory.CreateDirectory(physical);
        File.WriteAllText(Path.Combine(physical, name), content);
    }
}
