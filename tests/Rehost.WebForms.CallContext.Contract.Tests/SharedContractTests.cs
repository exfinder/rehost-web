using Shouldly;
using Xunit;

namespace Rehost.WebForms.CallContext.Contract.Tests;

// The parts of the CallContext contract both implementations honor. They exist so a failure in
// IllogicalIsolationTests cannot be blamed on the harness: the same shim, threads, and flows
// measure the same thing on both targets.
public sealed class SharedContractTests(ITestOutputHelper output)
{
    [Fact]
    public void Reports_The_Implementation_Under_Test() =>
        output.WriteLine("CallContext implementation: " + Cc.Implementation);

    [Fact]
    public void Illogical_Data_Is_Visible_To_Synchronous_Callees_On_The_Same_Thread()
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
    public void Illogical_Data_Does_Not_Reach_A_New_Thread()
    {
        var name = Guid.NewGuid().ToString("N");
        Cc.SetData(name, "parent");

        Threads.OnDedicated(() => Cc.GetData(name)).ShouldBeNull();

        Cc.FreeNamedDataSlot(name);
    }

    [Fact]
    public void Illogical_HostContext_Does_Not_Reach_A_New_Thread()
    {
        Cc.HostContext = "parent";

        Threads.OnDedicated(() => Cc.HostContext).ShouldBeNull();

        Cc.HostContext = null;
    }

    [Fact]
    public async Task Logical_Data_Flows_Into_A_Task_And_Child_Writes_Do_Not_Leak_Back()
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
    public void Parent_Illogical_Data_Is_Restored_After_A_Nested_Execution_Context_Run()
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
    public void FreeNamedDataSlot_Clears_Logical_And_Illogical_Storage()
    {
        var name = Guid.NewGuid().ToString("N");
        Cc.LogicalSetData(name, "logical");
        Cc.SetData(name, "illogical");

        Cc.FreeNamedDataSlot(name);

        Cc.GetData(name).ShouldBeNull();
        Cc.LogicalGetData(name).ShouldBeNull();
    }
}
