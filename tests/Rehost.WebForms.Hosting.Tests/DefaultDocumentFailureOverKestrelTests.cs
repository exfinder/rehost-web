using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// A broken <defaultDocument> section fails at consumption, not activation (readings D11/D13):
// the app starts, direct requests serve, and every directory request answers the config error.
public sealed class DefaultDocumentFailureOverKestrelTests(DefDocBrokenLiveScenario scenario)
    : IClassFixture<DefDocBrokenLiveScenario>
{
    [Fact]
    public async Task Directory_Requests_Fail_With_The_Config_Error()
    {
        var response = await scenario.Client.GetAsync("/");

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("Default.htm");
        response.Text.ShouldContain("duplicates");
    }

    [Fact]
    public async Task A_Slashless_Directory_Url_Fails_Without_Redirecting()
    {
        var response = await scenario.Client.GetAsync("/sub");

        response.StatusCode.ShouldBe(500);
        response.Header("Location").ShouldBeNull();
    }

    // The dynamic probe walks the full pipeline — through the directory-request step — so it
    // fails if the step consults the broken section for non-directory paths (reading D13).
    [Fact]
    public async Task Direct_Requests_Keep_Serving_Beside_The_Broken_Section()
    {
        var file = await scenario.Client.GetAsync("/note.txt");
        var handler = await scenario.Client.GetAsync("/probe");

        file.StatusCode.ShouldBe(200);
        file.Text.ShouldBe("note-content\n");
        handler.StatusCode.ShouldBe(200);
        handler.Text.ShouldBe("scenario");
    }
}
