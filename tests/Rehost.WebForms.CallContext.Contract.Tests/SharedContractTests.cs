using Shouldly;
using Xunit;

namespace Rehost.WebForms.CallContext.Contract.Tests;

// The parts of the CallContext contract both implementations honor. They exist so a failure in
// IllogicalIsolationTests cannot be blamed on the harness: the same shim, threads, and flows
// measure the same thing on both targets.
public sealed class SharedContractTests(ITestOutputHelper output)
{
    [Fact]
    public void Reports_the_implementation_under_test() =>
        output.WriteLine("CallContext implementation: " + Cc.Implementation);

    [Fact]
    public void Illogical_data_is_visible_to_synchronous_callees_on_the_same_thread()
    {
        Threads.OnDedicated(() =>
        {
            var name = Guid.NewGuid().ToString("N");
            Cc.SetData(name, "value");
            Callee(name).ShouldBe("value");
            Cc.FreeNamedDataSlot(name);
        });

        static object? Callee(string name) => Cc.GetData(name);
    }

    [Fact]
    public void Illogical_data_does_not_reach_a_new_thread()
    {
        var name = Guid.NewGuid().ToString("N");
        Cc.SetData(name, "parent");

        Threads.OnDedicated(() => Cc.GetData(name)).ShouldBeNull();

        Cc.FreeNamedDataSlot(name);
    }

    [Fact]
    public void Illogical_HostContext_does_not_reach_a_new_thread()
    {
        Cc.HostContext = "parent";

        Threads.OnDedicated(() => Cc.HostContext).ShouldBeNull();

        Cc.HostContext = null;
    }

    [Fact]
    public async Task Logical_data_flows_into_a_task_and_child_writes_do_not_leak_back()
    {
        var name = Guid.NewGuid().ToString("N");
        Cc.LogicalSetData(name, "parent");

        var inherited = await Task.Run(() =>
        {
            var seen = Cc.LogicalGetData(name);
            Cc.LogicalSetData(name, "child");
            return seen;
        });

        inherited.ShouldBe("parent");
        Cc.LogicalGetData(name).ShouldBe("parent");
        Cc.FreeNamedDataSlot(name);
    }

    [Fact]
    public void Parent_illogical_data_is_restored_after_a_nested_execution_context_run()
    {
        Threads.OnDedicated(() =>
        {
            var name = Guid.NewGuid().ToString("N");
            var child = Threads.Pristine();
            Cc.SetData(name, "parent");
            object? insideChild = "unset";

            ExecutionContext.Run(child, _ => insideChild = Cc.GetData(name), null);

            insideChild.ShouldBeNull();
            Cc.GetData(name).ShouldBe("parent");
            Cc.FreeNamedDataSlot(name);
        });
    }

    [Fact]
    public void FreeNamedDataSlot_clears_logical_and_illogical_storage()
    {
        var name = Guid.NewGuid().ToString("N");
        Cc.LogicalSetData(name, "logical");
        Cc.SetData(name, "illogical");

        Cc.FreeNamedDataSlot(name);

        Cc.GetData(name).ShouldBeNull();
        Cc.LogicalGetData(name).ShouldBeNull();
    }
}
