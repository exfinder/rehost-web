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

    // The security case: a folder maps everything to the forbidden handler, and that mapping
    // reaches requests into the folder and nothing outside it.
    [Fact]
    public async Task A_Folder_Scoped_Forbidden_Mapping_Blocks_Inside_The_Folder_Only()
    {
        var blocked = await scenario.Client.GetAsync("/guarded/secret.aspx");

        blocked.StatusCode.ShouldBe(403);
        blocked.Text.ShouldNotContain("secret-page");

        var outside = await scenario.Client.GetAsync("/secret.aspx");

        outside.StatusCode.ShouldBe(200);
        outside.Text.ShouldContain("secret-page");
    }

    // MH27a: the folder's own add is consulted ahead of the application root's for the same URL,
    // and the page on disk beside it is not what answered.
    [Fact]
    public async Task A_Folder_Add_Beats_The_Application_Root_For_The_Same_Pattern()
    {
        var inside = await scenario.Client.GetAsync("/deep/Default.aspx");

        inside.StatusCode.ShouldBe(200);
        inside.Text.ShouldBe("HANDLED-BY:B");

        (await scenario.Client.GetAsync("/Default.aspx")).Text.ShouldBe("HANDLED-BY:A");
    }

    // The SCRIPT_NAME/PATH_INFO split asks the folder's list as well: only inside the folder does
    // a mapping claim the .axd prefix, so only there does the URL continuing past it reach that
    // handler instead of staying one file path.
    [Fact]
    public async Task The_Path_Info_Split_Consults_The_Folder_List()
    {
        (await scenario.Client.GetAsync("/deep/thing.axd/extra")).Text.ShouldBe("HANDLED-BY:A");
        (await scenario.Client.GetAsync("/thing.axd/extra")).Text.ShouldBe("HANDLED-BY:B");
    }

    // MH27b: the folder removes the application root's add, matching there falls back to the
    // inherited page factory, and the root keeps its own mapping.
    [Fact]
    public async Task A_Folder_Remove_Falls_Back_To_The_Inherited_Page_Factory()
    {
        var inside = await scenario.Client.GetAsync("/sub/Default.aspx");

        inside.StatusCode.ShouldBe(200);
        inside.Text.ShouldContain("sub-page");

        (await scenario.Client.GetAsync("/Default.aspx")).Text.ShouldBe("HANDLED-BY:A");
    }
}
