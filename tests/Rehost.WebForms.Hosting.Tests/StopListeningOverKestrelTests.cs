using Rehost.WebForms.ScenarioProtocol;
using Rehost.WebForms.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// IV30: an integrated pool stop raises HostingEnvironment.StopListening and
// IStopListeningRegisteredObject.StopListening; an app-domain recycle raises neither, on either
// pool. The host's stopping notification is the port's equivalent of the pool stop, so the claim
// is about a whole host and needs its own process.
public sealed class StopListeningOverKestrelTests
{
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task A_Host_Initiated_Stop_Raises_Both_Stop_Listening_Signals()
    {
        using var log = new TempDirectory("rehost-stoplistening-");
        var path = log.Path("shutdown.log");
        using var scenario = Start(path);

        var warm = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        var stopping = await scenario.Client.GetAsync(HostControlProtocol.StopPath);

        warm.StatusCode.ShouldBe(200);
        stopping.StatusCode.ShouldBe(200);
        scenario.WaitForExit(ExitWait).ShouldBeTrue();
        var recorded = TraceChannel.ReadLines(path);
        var raised = recorded.IndexOf(ShutdownProtocol.StopListeningEvent);
        var listener = recorded.IndexOf(ShutdownProtocol.StopListeningObject);
        var stopped = recorded.IndexOf(ShutdownProtocol.RegisteredStop + "False");

        raised.ShouldBeGreaterThanOrEqualTo(0);
        listener.ShouldBeGreaterThanOrEqualTo(0);
        stopped.ShouldBeGreaterThan(raised);
        stopped.ShouldBeGreaterThan(listener);
    }

    [Fact]
    public async Task A_Throwing_Stop_Listening_Subscriber_Leaves_The_Teardown_Intact()
    {
        using var log = new TempDirectory("rehost-stoplistening-");
        var path = log.Path("shutdown.log");
        using var scenario = Start(path, throwingSubscriber: true);

        var warm = await scenario.Client.GetAsync(ProbePaths.ScenarioDefault);
        var stopping = await scenario.Client.GetAsync(HostControlProtocol.StopPath);

        warm.StatusCode.ShouldBe(200);
        stopping.StatusCode.ShouldBe(200);
        scenario.WaitForExit(ExitWait).ShouldBeTrue();
        var recorded = TraceChannel.ReadLines(path);
        recorded.ShouldContain(ShutdownProtocol.StopListeningThrew);
        recorded.ShouldNotContain(ShutdownProtocol.StopListeningEvent);
        recorded.ShouldContain(ShutdownProtocol.RegisteredStop + "False");
    }

    // The recycle line proves the same log channel was live in this process, so the two negatives
    // below cannot pass because nothing was ever written.
    [Fact]
    public async Task An_Application_Requested_Recycle_Raises_Neither()
    {
        using var log = new TempDirectory("rehost-stoplistening-");
        var path = log.Path("shutdown.log");
        using var scenario = Start(path);

        var unloading = await scenario.Client.GetAsync(ProbePaths.Unload);

        unloading.StatusCode.ShouldBe(200);
        scenario.WaitForExit(ExitWait).ShouldBeTrue();
        var recorded = TraceChannel.ReadLines(path);
        recorded.ShouldContain(ShutdownProtocol.RecycleRequested);
        recorded.ShouldNotContain(ShutdownProtocol.StopListeningEvent);
        recorded.ShouldNotContain(ShutdownProtocol.StopListeningObject);
    }

    private static LiveScenario Start(string logPath, bool throwingSubscriber = false)
    {
        var environment = new Dictionary<string, string>
        {
            [ShutdownProtocol.LogVariable] = logPath,
        };

        if (throwingSubscriber)
        {
            environment[ShutdownProtocol.ThrowingSubscriberVariable] = "1";
        }

        return LiveScenario.StartIsolated(
            Fixtures.AppStart, IsolationReason.ProcessDamage, environment: environment);
    }
}
