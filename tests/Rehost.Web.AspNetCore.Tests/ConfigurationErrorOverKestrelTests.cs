using Rehost.Web.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// Framework answers a web.config it cannot parse with 500 and the Configuration Error page naming
// the file and the failing line (config-error readings, 2026-09-07; ledger P96).
public sealed class ConfigurationErrorOverKestrelTests
{
    private const int RestartRequested = 82;

    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task A_Broken_Web_Config_Renders_The_Configuration_Error_Page_Then_Ends_The_Process()
    {
        using var scenario = LiveScenario.StartIsolated(Fixtures.ConfigError, IsolationReason.ProcessDamage);

        var response = await scenario.Client.GetAsync("/");
        var exited = scenario.WaitForExit(ExitWait);

        response.StatusCode.ShouldBe(500);
        response.Text.ShouldContain("<title>Configuration Error</title>", Case.Sensitive);
        response.Text.ShouldContain("Unrecognized configuration section madeUpSection.", Case.Sensitive);
        response.Text.ShouldContain("web.config", Case.Sensitive);
        exited.ShouldBeTrue();
        scenario.ExitCode.ShouldBe(RestartRequested);
    }
}
