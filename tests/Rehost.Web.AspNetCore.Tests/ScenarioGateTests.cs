using Rehost.Web.ScenarioProtocol;
using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// The gate parks application code in one process while a test in another observes that it is
// parked; both ends of that handshake run here in-process, which the pipe cannot tell apart.
public sealed class ScenarioGateTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan Brief = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task An_Arrival_Parks_Until_The_Gate_Is_Released()
    {
        var token = TestContext.Current.CancellationToken;
        using var gate = new ScenarioGate();
        using var waiter = ScenarioGate.Open(gate.Name);

        var parked = Task.Run(() => waiter.ArriveAndWait(Wait), token);
        await gate.WaitForArrivalAsync(Wait, token);

        parked.IsCompleted.ShouldBeFalse();

        gate.Release();
        await parked.WaitAsync(Wait, token);
    }

    [Fact]
    public async Task Disposing_A_Gate_Releases_An_Arrival_The_Test_Abandoned()
    {
        var token = TestContext.Current.CancellationToken;
        var gate = new ScenarioGate();
        using var waiter = ScenarioGate.Open(gate.Name);

        var parked = Task.Run(() => waiter.ArriveAndWait(Wait), token);
        await gate.WaitForArrivalAsync(Wait, token);

        gate.Dispose();

        await parked.WaitAsync(Wait, token);
    }

    [Fact]
    public void An_Unarmed_Waiter_Passes_Straight_Through()
    {
        using var waiter = ScenarioGate.Open(null);

        waiter.Armed.ShouldBeFalse();
        waiter.ArriveAndWait(Brief);
    }

    [Fact]
    public async Task A_Gate_No_One_Reaches_Reports_The_Phase_It_Stopped_At()
    {
        using var gate = new ScenarioGate();

        var timeout = await Should.ThrowAsync<TimeoutException>(
            () => gate.WaitForArrivalAsync(Brief, TestContext.Current.CancellationToken));

        timeout.Message.ShouldContain(gate.Name);
        timeout.Message.ShouldContain("the application never connected");
    }

    [Fact]
    public async Task An_Arrival_Nobody_Releases_Fails_Inside_Its_Own_Budget()
    {
        var token = TestContext.Current.CancellationToken;
        using var gate = new ScenarioGate();
        using var waiter = ScenarioGate.Open(gate.Name);

        var parked = Task.Run(() => waiter.ArriveAndWait(Brief), token);
        await gate.WaitForArrivalAsync(Wait, token);

        var timeout = await Should.ThrowAsync<TimeoutException>(() => parked);
        timeout.Message.ShouldContain("was never released");
    }

    [Fact]
    public async Task A_Gate_Takes_One_Arrival_And_One_Release()
    {
        var token = TestContext.Current.CancellationToken;
        using var gate = new ScenarioGate();
        using var waiter = ScenarioGate.Open(gate.Name);

        Should.Throw<InvalidOperationException>(gate.Release)
            .Message.ShouldContain(ScenarioGate.GateVariable);

        var parked = Task.Run(() => waiter.ArriveAndWait(Wait), token);
        await gate.WaitForArrivalAsync(Wait, token);
        gate.Release();
        await parked.WaitAsync(Wait, token);

        Should.Throw<InvalidOperationException>(gate.Release)
            .Message.ShouldContain("already released");
        Should.Throw<InvalidOperationException>(() => waiter.ArriveAndWait(Wait))
            .Message.ShouldContain("already been arrived at");
        (await Should.ThrowAsync<InvalidOperationException>(
            () => gate.TryWaitForArrivalAsync(Wait, token)))
            .Message.ShouldContain("already been waited on");
    }
}
