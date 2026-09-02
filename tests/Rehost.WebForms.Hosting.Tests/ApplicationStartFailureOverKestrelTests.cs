using System.Diagnostics;
using System.Text;
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

    private static readonly TimeSpan StartWait = TimeSpan.FromSeconds(30);

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
        failed.Text.ShouldContain($"{AppStartProtocol.FaultText}:1");
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
        first.Text.ShouldContain($"{AppStartProtocol.FaultText}:1");
        second.StatusCode.ShouldBe(500);
        second.Text.ShouldContain($"{AppStartProtocol.FaultText}:1");
    }

    // A waiter sent without the gate, or over a client that may not have dispatched it, can
    // reach the FirstRequestInit gate instead and pass this test without ever parking.
    [Fact]
    public async Task A_Request_Waiting_On_A_Slow_Failing_Start_Gets_The_Failure()
    {
        var token = TestContext.Current.CancellationToken;
        using var fault = new TempDirectory("rehost-appstart-");
        var marker = fault.Path("fault");
        File.WriteAllText(marker, "");
        using var gate = new ScenarioGate();
        using var scenario = Start(marker, gate);

        var runner = scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        await gate.WaitForArrivalAsync(StartWait, token);
        var waiter = await RawSocketProbe.DispatchRawGetAsync(
            scenario.Address, ProbePaths.ScenarioDefault);

        runner.IsCompleted.ShouldBeFalse();
        waiter.IsCompleted.ShouldBeFalse();

        gate.Release();

        var first = await runner;
        var parked = Encoding.ASCII.GetString(await waiter);

        first.StatusCode.ShouldBe(500);
        first.Text.ShouldContain($"{AppStartProtocol.FaultText}:1");
        parked.ShouldStartWith("HTTP/1.1 500");
        parked.ShouldContain($"{AppStartProtocol.FaultText}:1");
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

    private static LiveScenario Start(string marker, ScenarioGate? gate = null)
    {
        var environment = new Dictionary<string, string>
        {
            [AppStartProtocol.FaultMarkerVariable] = marker,
        };
        if (gate != null)
        {
            environment[ScenarioGate.GateVariable] = gate.Name;
        }

        return LiveScenario.StartIsolated(
            Fixtures.AppStart,
            IsolationReason.ProcessDamage,
            environment: environment);
    }
}
