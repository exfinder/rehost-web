using System.Diagnostics;
using System.Globalization;
using Rehost.WebForms.ScenarioProtocol;
using Rehost.WebForms.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Integrated mode latches an Application_Start failure and rebuilds the AppDomain ~10s later
// (winbox reading, 2026-08-30); the port's rebuild is a replacement process (ADR 0012).
public sealed class ApplicationStartFailureOverKestrelTests
{
    private const int RestartRequested = 82;

    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task A_Failed_Application_Start_Ends_The_Process_And_The_Next_One_Serves()
    {
        using var fault = new TempDirectory("rehost-appstart-");
        var marker = fault.Path("fault");
        File.WriteAllText(marker, "");
        using var scenario = Start(marker);

        var failed = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        var exited = scenario.WaitForExit(ExitWait);

        failed.StatusCode.ShouldBe(500);
        failed.Text.ShouldContain(AppStartProtocol.FaultText + "1");
        exited.ShouldBeTrue();
        scenario.ExitCode.ShouldBe(RestartRequested);

        File.Delete(marker);
        scenario.KillAndRestart();

        var served = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        served.StatusCode.ShouldBe(200);
        served.Text.ShouldBe("scenario");
    }

    // The run number proves the replay is the latched failure, not a fresh Application_Start.
    [Fact]
    public async Task Requests_Inside_The_Latch_Window_Replay_The_First_Failure()
    {
        using var fault = new TempDirectory("rehost-appstart-");
        var marker = fault.Path("fault");
        File.WriteAllText(marker, "");
        using var scenario = Start(marker);

        var first = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        var second = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);

        first.StatusCode.ShouldBe(500);
        first.Text.ShouldContain(AppStartProtocol.FaultText + "1");
        second.StatusCode.ShouldBe(500);
        second.Text.ShouldContain(AppStartProtocol.FaultText + "1");
    }

    // Waiters parked on the app-start lock get the latched failure, never partial init
    // (IIS integrated waiters all receive it; winbox reading, 2026-08-30).
    [Fact]
    public async Task A_Request_Waiting_On_A_Slow_Failing_Start_Gets_The_Failure()
    {
        using var fault = new TempDirectory("rehost-appstart-");
        var marker = fault.Path("fault");
        File.WriteAllText(marker, "");
        using var scenario = Start(marker, faultDelaySeconds: 4);

        var runner = scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
        var waiter = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        var first = await runner;

        first.StatusCode.ShouldBe(500);
        first.Text.ShouldContain(AppStartProtocol.FaultText + "1");
        waiter.StatusCode.ShouldBe(500);
        waiter.Text.ShouldContain(AppStartProtocol.FaultText + "1");
    }

    [Fact]
    public async Task A_Host_Initiated_Stop_Leaves_The_Exit_Code_Alone()
    {
        using var fault = new TempDirectory("rehost-appstart-");
        using var scenario = Start(fault.Path("fault"));

        var served = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        var stopping = await scenario.Client.GetAsync(HostControlProtocol.StopPath);

        served.StatusCode.ShouldBe(200);
        stopping.StatusCode.ShouldBe(200);
        scenario.WaitForExit(ExitWait).ShouldBeTrue();
        scenario.ExitCode.ShouldBe(0);
    }

    // No timer involved: an application-requested recycle must exit well inside the 10s window.
    [Fact]
    public async Task Application_Code_Unloading_The_AppDomain_Ends_The_Process()
    {
        using var fault = new TempDirectory("rehost-appstart-");
        using var scenario = Start(fault.Path("fault"));

        var unloading = await scenario.Client.GetAsync(ProbePaths.Unload);
        var elapsed = Stopwatch.StartNew();
        var exited = scenario.WaitForExit(TimeSpan.FromSeconds(8));

        unloading.StatusCode.ShouldBe(200);
        exited.ShouldBeTrue($"the host was still running after {elapsed.Elapsed}");
        scenario.ExitCode.ShouldBe(RestartRequested);
    }

    private static LiveScenario Start(string marker, int faultDelaySeconds = 0)
    {
        var environment = new Dictionary<string, string>
        {
            [AppStartProtocol.FaultMarkerVariable] = marker,
        };
        if (faultDelaySeconds > 0)
        {
            environment[AppStartProtocol.FaultDelaySecondsVariable] =
                faultDelaySeconds.ToString(CultureInfo.InvariantCulture);
        }

        return LiveScenario.StartIsolated(
            Fixtures.AppStart,
            IsolationReason.ProcessDamage,
            environment: environment);
    }
}
