using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// A disabled <defaultDocument> section owns the whole directory surface: it suppresses the
// courtesy redirect as well as the rewrite, so both slash forms refuse (readings D12/D14).
public sealed class DefaultDocumentDisabledOverKestrelTests(DefDocDisabledLiveScenario scenario)
    : IClassFixture<DefDocDisabledLiveScenario>
{
    [Fact]
    public async Task Directory_Requests_Refuse_Even_With_A_Candidate_Present()
    {
        (await scenario.Client.GetAsync("/")).StatusCode.ShouldBe(403);
        (await scenario.Client.GetAsync("/sub/")).StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task A_Slashless_Directory_Url_Refuses_Instead_Of_Redirecting()
    {
        var response = await scenario.Client.GetAsync("/sub");

        response.StatusCode.ShouldBe(403);
        response.Header("Location").ShouldBeNull();
    }

    [Fact]
    public async Task Direct_Requests_Keep_Serving()
    {
        var response = await scenario.Client.GetAsync("/Default.htm");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("disabled-root-default\n");
    }
}
