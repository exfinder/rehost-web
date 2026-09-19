using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class BaseDirectoryOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task The_Base_Directory_Is_The_Site_Root_With_A_Trailing_Separator()
    {
        var response = await scenario.Client.GetAsync(ProbePaths.BaseDirectory);

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldBe($"{scenario.ApplicationPath}{Path.DirectorySeparatorChar}");
    }
}
