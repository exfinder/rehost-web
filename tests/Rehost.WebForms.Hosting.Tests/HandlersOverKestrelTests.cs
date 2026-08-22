using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The merged <handlers> walk over a real socket. The matrix lives at the configuration seam; what
// these prove is that the walk is the pipeline's mapping authority end to end.
public sealed class HandlersOverKestrelTests(HandlersLiveScenario scenario)
    : IClassFixture<HandlersLiveScenario>
{
    // MH8: Default.aspx is on disk and would compile, and the application's own mapping takes it
    // from the inherited page factory anyway.
    [Fact]
    public async Task An_Application_Mapping_Beats_The_Inherited_Page_Factory()
    {
        var response = await scenario.Client.GetAsync("/Default.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("HANDLED-BY:A");
    }

    // MH9v: the re-added inherited name is active, and still matches behind the fresh adds.
    [Fact]
    public async Task A_Re_Added_Inherited_Name_Matches_Behind_The_Fresh_Adds()
    {
        (await scenario.Client.GetAsync("/api")).Text.ShouldBe("HANDLED-BY:A");
        (await scenario.Client.GetAsync("/nosuchthing")).Text.ShouldBe("HANDLED-BY:B");
    }

    // MH21: the verb-restricted row is skipped for GET and the walk continues; no 405.
    [Fact]
    public async Task A_Verb_Mismatched_Row_Falls_Through_Without_A_405()
    {
        var get = await scenario.Client.GetAsync("/verb.axd");
        get.StatusCode.ShouldBe(200);
        get.Text.ShouldBe("HANDLED-BY:B");

        var post = await scenario.Client.PostAsync("/verb.axd", Array.Empty<byte>(), "text/plain");
        post.StatusCode.ShouldBe(200);
        post.Text.ShouldBe("HANDLED-BY:A");
    }

    // MH22a: the type resolves at first match, so only the URL that reaches the row fails.
    [Fact]
    public async Task A_Bad_Type_Fails_Only_Its_Own_Url()
    {
        var broken = await scenario.Client.GetAsync("/broken.axd");

        broken.StatusCode.ShouldBe(500);
        broken.Text.ShouldContain("Broken");

        (await scenario.Client.GetAsync("/api")).StatusCode.ShouldBe(200);
    }

    // MH18: with the catch-all removed nothing matches an ordinary file URL, and the static file
    // on disk beside it does not change that.
    [Fact]
    public async Task An_Unmatched_Url_Answers_404_Once_The_Catch_All_Is_Removed()
    {
        var response = await scenario.Client.GetAsync("/asset.txt");

        response.StatusCode.ShouldBe(404);
        response.Text.ShouldNotContain("STATIC-OK");
    }
}
