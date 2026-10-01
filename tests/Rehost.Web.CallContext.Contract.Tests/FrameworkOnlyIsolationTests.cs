#if NET481
using Shouldly;
using Xunit;

namespace Rehost.Web.CallContext.Contract.Tests;

// The half of Framework's illogical contract the port cannot meet: a continuation captured while
// a scope still holds illogical data, landing back on the thread that captured it. mscorlib keeps
// illogical data in the ExecutionContext and does not copy it on capture, so the flow sees
// nothing; over AsyncLocal the port cannot tell that flow from a return out of a nested Run, so
// it shows the scope's own data. Unreachable through System.Web (every caller clears before the
// thread leaves); stated in docs/dev/call-context-compatibility.md. Compiled for the Framework leg
// only, so the reading of the boundary is what this file pins.
public sealed class FrameworkOnlyIsolationTests
{
    [Fact]
    public void Illogical_HostContext_Is_Not_Visible_When_The_Captured_Context_Runs_On_The_Origin_Thread()
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
    public void Illogical_Data_Is_Not_Visible_When_The_Captured_Context_Runs_On_The_Origin_Thread()
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
    public void Illogical_Writes_Made_After_Capture_Are_Not_Visible_Inside_The_Flow()
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
    public void Two_Continuations_Of_One_Flow_Landing_On_The_Origin_Thread_Never_See_Illogical_HostContext()
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
}
#endif
