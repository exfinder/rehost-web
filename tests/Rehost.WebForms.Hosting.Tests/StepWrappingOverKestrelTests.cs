using Rehost.WebForms.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// OnExecuteRequestStep wraps every step of the classic list, which already routes through
// _stepInvoker; only the integrated gate refused the registration.
public sealed class StepWrappingOverKestrelTests(ModulesLiveScenario scenario)
    : IClassFixture<ModulesLiveScenario>
{
    private const string WrappedPrefix = "wrapped:";

    [Fact]
    public async Task A_Module_Wraps_Every_Pipeline_Step_Of_A_Request()
    {
        var (response, stages) = await scenario.TracedGetAsync(this, "/Default.aspx?wrap=1");

        response.StatusCode.ShouldBe(200);
        var wrapped = stages
            .Where(stage => stage.StartsWith(WrappedPrefix, StringComparison.Ordinal))
            .ShouldHaveSingleItem();
        int.Parse(wrapped[WrappedPrefix.Length..]).ShouldBeGreaterThan(1);
    }

    // IV18: a module's Init on a request instance reads BeginRequest; the application instances
    // are built by the host's first requests, so the record exists before any scenario runs.
    [Fact]
    public async Task Module_Init_On_A_Request_Instance_Reads_BeginRequest()
    {
        var witness = await scenario.Client.GetAsync(ProbePaths.Witness);

        witness.Text.Split('\n').ShouldContain(
            WitnessProtocol.InitNotificationPrefix + "BeginRequest/False");
    }
}
