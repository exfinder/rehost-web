using Shouldly;
using Xunit;

namespace Rehost.WebForms.CallContext.Contract.Tests;

// .NET Framework keeps illogical call-context data (SetData without ILogicalThreadAffinative, and
// the non-affinative HostContext behind HttpContext.Current) in the ExecutionContext and does not
// copy it on capture: a flowed continuation never sees it, wherever it runs, and a work item's
// writes die with the work item's copy-on-write scope. Every test here is phrased against that
// contract.
//
// The deterministic tests model thread-pool dispatch exactly: a dedicated thread runs work item 1
// under ExecutionContext.Run (the request sets its context and an await captures it), returns to
// idle, then runs work item 2 under the captured context (the continuation landing on the same
// thread). A SynchronizationContext is installed in work item 1 as AspNetSynchronizationContext is
// on every request thread; see Bare_HostContext_... for why that matters on Framework.
public sealed class IllogicalIsolationTests(ITestOutputHelper output)
{
    // ---- Deterministic: a flow that returns to the very thread that captured it ----------------

    [Fact]
    public void Illogical_HostContext_is_not_visible_when_the_captured_context_runs_on_the_origin_thread()
    {
        Threads.OnDedicated(() =>
        {
            var context = new object();
            ExecutionContext? captured = null;
            object? seenInsideFlow = "unset";

            ExecutionContext.Run(Threads.Pristine(), _ =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                Cc.HostContext = context;
                captured = ExecutionContext.Capture();
            }, null);
            ExecutionContext.Run(captured!, _ => seenInsideFlow = Cc.HostContext, null);

            seenInsideFlow.ShouldBeNull();
        });
    }

    [Fact]
    public void Illogical_data_is_not_visible_when_the_captured_context_runs_on_the_origin_thread()
    {
        Threads.OnDedicated(() =>
        {
            var name = Guid.NewGuid().ToString("N");
            ExecutionContext? captured = null;
            object? seenInsideFlow = "unset";

            ExecutionContext.Run(Threads.Pristine(), _ =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                Cc.SetData(name, "request-A");
                captured = ExecutionContext.Capture();
            }, null);
            ExecutionContext.Run(captured!, _ => seenInsideFlow = Cc.GetData(name), null);

            seenInsideFlow.ShouldBeNull();
        });
    }

    [Fact]
    public void Illogical_writes_made_after_capture_are_not_visible_inside_the_flow()
    {
        Threads.OnDedicated(() =>
        {
            var name = Guid.NewGuid().ToString("N");
            ExecutionContext? captured = null;
            object? seenInsideFlow = "unset";

            ExecutionContext.Run(Threads.Pristine(), _ =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                Cc.SetData(name, "before-capture");
                captured = ExecutionContext.Capture();
                Cc.SetData(name, "after-capture");
            }, null);
            ExecutionContext.Run(captured!, _ => seenInsideFlow = Cc.GetData(name), null);

            seenInsideFlow.ShouldNotBe("after-capture");
        });
    }

    [Fact]
    public void Two_continuations_of_one_flow_landing_on_the_origin_thread_never_see_illogical_HostContext()
    {
        Threads.OnDedicated(() =>
        {
            var context = new object();
            ExecutionContext? first = null;
            ExecutionContext? second = null;
            var seen = new List<object?>();

            ExecutionContext.Run(Threads.Pristine(), _ =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                Cc.HostContext = context;
                first = ExecutionContext.Capture();
                second = ExecutionContext.Capture();
            }, null);
            ExecutionContext.Run(first!, _ => seen.Add(Cc.HostContext), null);
            ExecutionContext.Run(second!, _ => seen.Add(Cc.HostContext), null);

            seen.ShouldAllBe(v => v == null);
        });
    }

    [Fact]
    public void A_work_item_that_leaves_illogical_HostContext_behind_does_not_hand_it_to_the_next_work_item()
    {
        Threads.OnDedicated(() =>
        {
            object? seenByNext = "unset";

            ExecutionContext.Run(Threads.Pristine(), _ =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                Cc.HostContext = new object();
            }, null);
            ExecutionContext.Run(Threads.Pristine(), _ => seenByNext = Cc.HostContext, null);

            seenByNext.ShouldBeNull();
        });
    }

    // Framework detail worth pinning rather than tripping over: mscorlib's
    // ExecutionContext.IsDefaultFTContext ignores a lone illogical HostContext (it checks the
    // illogical Datastore, the sync context, and logical data), so a thread carrying nothing but a
    // HostContext is not switched by Run at all and the value stays visible. Irrelevant to
    // ASP.NET, whose request threads always carry a SynchronizationContext, but it is what 4.8.1
    // does; recorded so nobody "fixes" the port into hiding it here and calls that Framework.
    [Fact]
    public void Bare_HostContext_with_no_other_context_stays_visible_across_Run_on_the_origin_thread()
    {
        Threads.OnDedicated(() =>
        {
            var context = new object();
            Cc.HostContext = context;
            var captured = ExecutionContext.Capture()!;
            object? seenInsideFlow = "unset";

            ExecutionContext.Run(Threads.Pristine(), _ =>
                ExecutionContext.Run(captured, _ => seenInsideFlow = Cc.HostContext, null), null);

            output.WriteLine("bare HostContext inside Run: " + (seenInsideFlow == null ? "null" : "visible"));
            seenInsideFlow.ShouldBeSameAs(context);
            Cc.HostContext = null;
        });
    }

    // ---- Statistical: the real thread pool ------------------------------------------------------

    [Fact]
    public async Task HostContext_is_null_after_ConfigureAwait_false_on_every_pool_thread()
    {
        const int Flows = 4000;
        var nonNull = 0;

        await Task.WhenAll(Enumerable.Range(0, Flows).Select(_ => Task.Run(async () =>
        {
            Cc.HostContext = new object();
            await Task.Delay(1).ConfigureAwait(false);
            if (Cc.HostContext != null)
            {
                Interlocked.Increment(ref nonNull);
            }
            Cc.HostContext = null;
        })));

        output.WriteLine($"HostContext non-null after ConfigureAwait(false): {nonNull}/{Flows}");
        nonNull.ShouldBe(0);
    }

    [Fact]
    public async Task Illogical_data_is_null_after_ConfigureAwait_false_on_every_pool_thread()
    {
        const int Flows = 4000;
        var name = Guid.NewGuid().ToString("N");
        var nonNull = 0;

        await Task.WhenAll(Enumerable.Range(0, Flows).Select(_ => Task.Run(async () =>
        {
            Cc.SetData(name, "request");
            await Task.Delay(1).ConfigureAwait(false);
            if (Cc.GetData(name) != null)
            {
                Interlocked.Increment(ref nonNull);
            }
            Cc.FreeNamedDataSlot(name);
        })));

        output.WriteLine($"illogical data non-null after ConfigureAwait(false): {nonNull}/{Flows}");
        nonNull.ShouldBe(0);
    }

    // ---- Retention: nothing survives the flow ---------------------------------------------------

    [Fact]
    public async Task HostContext_objects_are_collectible_once_their_flows_have_completed()
    {
        const int Flows = 2000;
        var references = new List<WeakReference>();

        await Task.WhenAll(Enumerable.Range(0, Flows).Select(_ => Task.Run(async () =>
        {
            var context = new byte[16 * 1024];
            lock (references) references.Add(new WeakReference(context));
            Cc.HostContext = context;
            await Task.Yield();
            await Task.Delay(1).ConfigureAwait(false);
            Cc.HostContext = null;
        })));
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        var alive = references.Count(r => r.IsAlive);
        output.WriteLine($"HostContext objects alive after GC: {alive}/{Flows}");
        // A handful may still sit in a live thread's current context; thousands cannot.
        alive.ShouldBeLessThan(Flows / 100);
    }

    [Fact]
    public async Task Illogical_data_values_are_collectible_once_their_flows_have_completed()
    {
        const int Flows = 2000;
        var name = Guid.NewGuid().ToString("N");
        var references = new List<WeakReference>();

        await Task.WhenAll(Enumerable.Range(0, Flows).Select(_ => Task.Run(async () =>
        {
            var value = new byte[16 * 1024];
            lock (references) references.Add(new WeakReference(value));
            Cc.SetData(name, value);
            await Task.Yield();
            await Task.Delay(1).ConfigureAwait(false);
            Cc.FreeNamedDataSlot(name);
        })));
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        var alive = references.Count(r => r.IsAlive);
        output.WriteLine($"illogical values alive after GC: {alive}/{Flows}");
        alive.ShouldBeLessThan(Flows / 100);
    }
}
