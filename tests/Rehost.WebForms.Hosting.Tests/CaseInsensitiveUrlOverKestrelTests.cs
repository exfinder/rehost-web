using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Guards the Framework contract that URL casing never matters (ledger P57): on a case-sensitive
// filesystem the mapped path misses and the canonical-case resolution must fold it, end to end
// through Kestrel, compilation, and the response. The fixture sits on a real case-sensitive
// volume; where the platform cannot provide one the tests skip — Windows already proves the
// insensitive behavior natively.
public sealed class CaseInsensitiveUrlOverKestrelTests(CaseSensitiveLiveScenario scenario)
{
    // The page is created here and requested wrongly-cased first, so no other request in the
    // class can have warmed a cache under any casing: once one casing compiles, the build
    // cache's own case-insensitive key serves the rest without touching the filesystem, which
    // would mask a missing resolution.
    [Fact]
    public async Task A_Wrongly_Cased_Url_Serves_The_Page()
    {
        var live = scenario.RequireLive();
        File.WriteAllText(
            Path.Combine(live.ApplicationPath, "Folded.aspx"),
            """<%@ Page Language="C#" %>folded-page""");

        var folded = await live.Client.GetAsync("/folded.aspx");
        var shouting = await live.Client.GetAsync("/FOLDED.ASPX");
        var exact = await live.Client.GetAsync("/Folded.aspx");

        folded.StatusCode.ShouldBe(200);
        folded.Text.ShouldBe("folded-page");
        shouting.StatusCode.ShouldBe(200);
        exact.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task A_Case_Collision_Fails_With_Both_Names_Rather_Than_Guessing()
    {
        var live = scenario.RequireLive();
        File.WriteAllText(
            Path.Combine(live.ApplicationPath, "Extra.aspx"),
            """<%@ Page Language="C#" %>upper""");
        File.WriteAllText(
            Path.Combine(live.ApplicationPath, "extra.aspx"),
            """<%@ Page Language="C#" %>lower""");

        var ambiguous = await live.Client.GetAsync("/EXTRA.aspx");
        var exact = await live.Client.GetAsync("/extra.aspx");

        ambiguous.StatusCode.ShouldBe(500);
        ambiguous.Text.ShouldContain("Extra.aspx");
        ambiguous.Text.ShouldContain("extra.aspx");
        exact.StatusCode.ShouldBe(200);
        exact.Text.ShouldContain("lower");
    }

    [Fact]
    public async Task A_Genuinely_Missing_Page_Still_Renders_The_404()
    {
        var live = scenario.RequireLive();

        var response = await live.Client.GetAsync("/nothere.aspx");

        response.StatusCode.ShouldBe(404);
    }

    // A static file reaches the filesystem through the worker request's own concatenation rather
    // than MapPathActual, so it needs the fold applied where that path enters the request.
    [Fact]
    public async Task A_Wrongly_Cased_Static_File_Url_Serves()
    {
        var live = scenario.RequireLive();
        var directory = Path.Combine(live.ApplicationPath, "Assets", "Inner");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Site.js"), "//static-body");

        var foldedFile = await live.Client.GetAsync("/Assets/Inner/site.js");
        var foldedDirectory = await live.Client.GetAsync("/assets/INNER/Site.js");
        var exact = await live.Client.GetAsync("/Assets/Inner/Site.js");

        foldedFile.StatusCode.ShouldBe(200);
        foldedFile.Text.ShouldContain("static-body");
        foldedDirectory.StatusCode.ShouldBe(200);
        foldedDirectory.Text.ShouldContain("static-body");
        exact.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task A_Static_File_Case_Collision_Fails_With_Both_Names()
    {
        var live = scenario.RequireLive();
        File.WriteAllText(Path.Combine(live.ApplicationPath, "Dup.js"), "//upper");
        File.WriteAllText(Path.Combine(live.ApplicationPath, "dup.js"), "//lower");

        var ambiguous = await live.Client.GetAsync("/DUP.js");
        var exact = await live.Client.GetAsync("/dup.js");

        ambiguous.StatusCode.ShouldBe(500);
        ambiguous.Text.ShouldContain("Dup.js");
        ambiguous.Text.ShouldContain("dup.js");
        exact.StatusCode.ShouldBe(200);
        exact.Text.ShouldContain("//lower");
    }

    [Fact]
    public async Task A_Genuinely_Missing_Static_File_Still_Answers_404()
    {
        var live = scenario.RequireLive();

        var response = await live.Client.GetAsync("/Assets/nothere.js");

        response.StatusCode.ShouldBe(404);
    }
}
