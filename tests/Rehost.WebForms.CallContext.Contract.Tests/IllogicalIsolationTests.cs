using Shouldly;
using Xunit;

namespace Rehost.WebForms.CallContext.Contract.Tests;

// The illogical-isolation contract both implementations honor: data left on a thread does not
// reach the next work item, nothing survives a completed flow, and Framework's bare-thread
// quirk. The Framework-only half of the contract is in FrameworkOnlyIsolationTests.
//
// The deterministic tests model thread-pool dispatch: a dedicated thread runs a work item under
// ExecutionContext.Run, returns to idle, then runs the next work item. A SynchronizationContext is
// installed as AspNetSynchronizationContext is on every request thread; see
// Bare_HostContext_... for why that matters on Framework.
public sealed class IllogicalIsolationTests(ITestOutputHelper output)
{
    [Fact]
    public void A_Work_Item_That_Leaves_Illogical_HostContext_Behind_Does_Not_Hand_It_To_The_Next_Work_Item()
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
    public void Bare_HostContext_With_No_Other_Context_Stays_Visible_Across_Run_On_The_Origin_Thread()
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

    // ---- The real thread pool --------------------------------------------------------------------
    //
    // Neither implementation is absolute here. mscorlib 4.8.9337 measured HostContext non-null
    // 2/4000 and 1/4000 and a data slot 1/4000 across ten 4000-flow runs (winbox, 2026-08-15); the
    // port measures 0-2/4000. Both are a same-scope value seen by its own continuation on the
    // origin thread. What the pool must never show is a gross leak, which the bound catches
    // (thousands, as an unbounded suspended stack produced). The data slot beside the HostContext
    // keeps Framework off its lone-HostContext fast path (see Bare_HostContext_...).
    [Fact]
    public async Task HostContext_After_ConfigureAwait_False_Is_Null_On_The_Pool_Beyond_A_Rare_Same_Scope_Echo()
    {
        const int Flows = 4000;
        var name = Guid.NewGuid().ToString("N");
        var nonNull = 0;

        await Task.WhenAll(Enumerable.Range(0, Flows).Select(_ => Task.Run(async () =>
        {
            Cc.HostContext = new object();
            Cc.SetData(name, "request");
            await Task.Delay(1).ConfigureAwait(false);
            if (Cc.HostContext != null)
            {
                Interlocked.Increment(ref nonNull);
            }
            Cc.HostContext = null;
            Cc.FreeNamedDataSlot(name);
        })));

        output.WriteLine($"HostContext non-null after ConfigureAwait(false): {nonNull}/{Flows}");
        nonNull.ShouldBeLessThan(Flows / 100);
    }

    [Fact]
    public async Task Illogical_Data_After_ConfigureAwait_False_Is_Null_On_The_Pool_Beyond_A_Rare_Same_Scope_Echo()
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
        nonNull.ShouldBeLessThan(Flows / 100);
    }

    // ---- Retention: nothing survives the flow ---------------------------------------------------

    [Fact]
    public async Task HostContext_Objects_Are_Collectible_Once_Their_Flows_Have_Completed()
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
        Collect();

        var alive = references.Count(r => r.IsAlive);
        output.WriteLine($"HostContext objects alive after GC: {alive}/{Flows}");
        // A handful may still sit in a live thread's current context; thousands cannot.
        alive.ShouldBeLessThan(Flows / 100);
    }

    [Fact]
    public async Task Illogical_Data_Values_Are_Collectible_Once_Their_Flows_Have_Completed()
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
        Collect();

        var alive = references.Count(r => r.IsAlive);
        output.WriteLine($"illogical values alive after GC: {alive}/{Flows}");
        alive.ShouldBeLessThan(Flows / 100);
    }

    private static void Collect()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
