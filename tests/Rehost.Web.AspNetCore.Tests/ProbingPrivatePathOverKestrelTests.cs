using Rehost.Web.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

public sealed class ProbingPrivatePathOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    [Fact]
    public async Task An_Assembly_Only_In_A_Probing_Folder_Loads_From_It()
    {
        var response = await scenario.Client.GetAsync(ProbePaths.Probing);

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.Replace(Path.DirectorySeparatorChar, '/')
            .ShouldEndWith("bin/probing/Rehost.Web.ScenarioProbes.Dynamic.dll", Case.Sensitive);
    }
}
