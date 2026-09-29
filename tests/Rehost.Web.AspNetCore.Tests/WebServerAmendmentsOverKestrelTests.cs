using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// App <system.webServer> amendments over the shipped baseline (ledger P60, readings C2-C6).
// The fixture carries an unhonored section on purpose - activation succeeding asserts tolerance.
public sealed class WebServerAmendmentsOverKestrelTests(WebServerLiveScenario scenario)
    : IClassFixture<WebServerLiveScenario>
{
    [Fact]
    public async Task An_Added_MimeMap_Serves_With_The_Configured_Type()
    {
        var response = await scenario.Client.GetAsync("/data.probe");

        response.StatusCode.ShouldBe(200);
        response.Header("Content-Type").ShouldBe("application/x-fixture-probe");
        response.Text.ShouldBe("probe-content\n");
    }

    [Fact]
    public async Task A_Removed_Extension_Refuses_Below_The_Baseline()
    {
        var response = await scenario.Client.GetAsync("/plain.css");

        response.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task An_Added_Hidden_Segment_Refuses_Its_Files()
    {
        var physical = Path.Combine(scenario.ApplicationPath, "Private");
        Directory.CreateDirectory(physical);
        File.WriteAllText(Path.Combine(physical, "secret.probe"), "secret");

        var response = await scenario.Client.GetAsync("/Private/secret.probe");

        response.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task A_Removed_Hidden_Segment_Serves_Again()
    {
        var physical = Path.Combine(scenario.ApplicationPath, "App_Data");
        Directory.CreateDirectory(physical);
        File.WriteAllText(Path.Combine(physical, "open.probe"), "open");

        var response = await scenario.Client.GetAsync("/App_Data/open.probe");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("open");
    }
}
