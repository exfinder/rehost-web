using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// IIS's DefaultDocumentModule decided directory requests, and the port restores its measured
// behavior (ledger P67, readings D1-D9): list-order probing with the list's own casing, app
// adds prepending, 301 for slash-less directory URLs, 403 when nothing serves.
public sealed class DefaultDocumentsOverKestrelTests(WebServerLiveScenario scenario)
    : IClassFixture<WebServerLiveScenario>
{
    [Fact]
    public async Task The_Root_Serves_Its_Default_Document_In_The_Lists_Casing()
    {
        var response = await scenario.Client.GetAsync("/?q=1&x=%20y");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldContain("path=/default.aspx\n");
        response.Text.ShouldContain("filepath=/default.aspx\n");
        response.Text.ShouldContain("apprelative=~/default.aspx\n");
        response.Text.ShouldContain("rawurl=/?q=1&x=%20y\n");
    }

    [Fact]
    public async Task List_Order_Decides_Between_Existing_Candidates()
    {
        var response = await scenario.Client.GetAsync("/sub-order/");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("order-default-htm\n");
    }

    [Fact]
    public async Task Missing_Candidates_Are_Skipped_Silently()
    {
        var response = await scenario.Client.GetAsync("/sub-probe/");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("probe-index-html\n");
    }

    [Fact]
    public async Task An_App_Add_Prepends_Over_The_Inherited_List()
    {
        var response = await scenario.Client.GetAsync("/sub-add/");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("add-probe-default\n");
    }

    [Fact]
    public async Task A_Removed_Candidate_No_Longer_Serves()
    {
        var response = await scenario.Client.GetAsync("/sub-removed/");

        response.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task A_Directory_With_No_Candidate_Refuses()
    {
        Directory.CreateDirectory(Path.Combine(scenario.ApplicationPath, "hollow"));

        var response = await scenario.Client.GetAsync("/hollow/");

        response.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task A_Slashless_Directory_Url_Redirects_Permanently_Keeping_The_Query()
    {
        var response = await scenario.Client.GetAsync("/sub-order?x=%20y");

        response.StatusCode.ShouldBe(301);
        response.Header("Location").ShouldBe(
            scenario.Address.GetLeftPart(UriPartial.Authority) + "/sub-order/?x=%20y");
    }

    [Fact]
    public async Task A_Missing_Directory_Stays_404_With_And_Without_A_Slash()
    {
        (await scenario.Client.GetAsync("/nope/")).StatusCode.ShouldBe(404);
        (await scenario.Client.GetAsync("/nope")).StatusCode.ShouldBe(404);
    }
}

// IIS answered the browsing-off 403 natively, before managed error handling, so an app's
// customErrors never converts it into the defaultRedirect; httpErrors is what writes its body,
// and this host carries no row for 403.
public sealed class DirectoryRefusalOverKestrelTests(CustomErrorsLiveScenario scenario)
    : IClassFixture<CustomErrorsLiveScenario>
{
    [Fact]
    public async Task Custom_Errors_Do_Not_Convert_The_Directory_Refusal()
    {
        Directory.CreateDirectory(Path.Combine(scenario.ApplicationPath, "hollow"));

        var response = await scenario.Client.GetAsync("/hollow/");

        response.StatusCode.ShouldBe(403);
        response.Text.ShouldContain(
            "You do not have permission to view this directory or page.", Case.Sensitive);
    }
}
