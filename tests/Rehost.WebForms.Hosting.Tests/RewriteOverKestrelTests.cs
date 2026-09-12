using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The IIS URL Rewrite module's inbound rules over the fixture's <rewrite> section: every pattern
// sits under rw/ so no other class's request on this host meets one (readings UR9, UR12, UR26,
// UR27, UR30).
public sealed class RewriteOverKestrelTests(WebServerLiveScenario scenario)
    : IClassFixture<WebServerLiveScenario>
{
    [Fact]
    public async Task A_Rewritten_Page_Keeps_The_Original_Url_And_Moves_Every_Other_Member()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/rw/clean/5");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("path=/rw-probe.aspx", Case.Sensitive);
        response.Text.ShouldContain("rawurl=/rw/clean/5?", Case.Sensitive);
        response.Text.ShouldContain("query=5", Case.Sensitive);
        response.Text.ShouldContain("xoriginal=/rw/clean/5?", Case.Sensitive);
        response.Text.ShouldContain("rewritten=1", Case.Sensitive);
        response.Text.ShouldContain("inallkeys=False", Case.Sensitive);
        response.Text.ShouldContain("""action="./5?id=5""", Case.Sensitive);
        stages.ShouldContain("BeginRequest");
    }

    // The rewritten URL faces request filtering again, so a rule cannot reach hidden content; the
    // un-hidden sibling is the control that the rule itself works (UR27).
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
