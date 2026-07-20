using Shouldly;
using System.Runtime.Remoting.Messaging;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

public sealed class CallContextTests
{
    [Fact]
    public void Illogical_data_does_not_flow_to_new_thread()
    {
        var name = Guid.NewGuid().ToString("N");
        CallContext.SetData(name, "parent");
        object? childValue = null;
        var thread = new Thread(() => childValue = CallContext.GetData(name));

        thread.Start();
        thread.Join();

        childValue.ShouldBeNull();
        CallContext.FreeNamedDataSlot(name);
    }

    [Fact]
    public void Parent_illogical_data_is_restored_after_child_execution_context()
    {
        var childContext = ExecutionContext.Capture();
        var name = Guid.NewGuid().ToString("N");
        CallContext.SetData(name, "parent");
        object? childValue = null;

        ExecutionContext.Run(childContext!, _ => childValue = CallContext.GetData(name), null);

        childValue.ShouldBeNull();
        CallContext.GetData(name).ShouldBe("parent");
        CallContext.FreeNamedDataSlot(name);
    }

    [Fact]
    public async Task Logical_data_flows_into_task_without_child_writes_leaking_back()
    {
        var name = Guid.NewGuid().ToString("N");
        CallContext.LogicalSetData(name, "parent");

        var inheritedValue = await Task.Run(() =>
        {
            var inherited = CallContext.LogicalGetData(name);
            CallContext.LogicalSetData(name, "child");
            return inherited;
        });

        inheritedValue.ShouldBe("parent");
        CallContext.LogicalGetData(name).ShouldBe("parent");
        CallContext.FreeNamedDataSlot(name);
    }

    [Fact]
    public async Task SetData_uses_logical_storage_for_affinative_values()
    {
        var name = Guid.NewGuid().ToString("N");
        var value = new LogicalValue();

        CallContext.SetData(name, value);

        (await Task.Run(() => CallContext.GetData(name))).ShouldBeSameAs(value);
        CallContext.LogicalGetData(name).ShouldBeSameAs(value);
        CallContext.FreeNamedDataSlot(name);
    }

    [Fact]
    public void FreeNamedDataSlot_clears_logical_and_illogical_storage()
    {
        var name = Guid.NewGuid().ToString("N");
        CallContext.LogicalSetData(name, "logical");
        CallContext.SetData(name, "illogical");

        CallContext.FreeNamedDataSlot(name);

        CallContext.GetData(name).ShouldBeNull();
        CallContext.LogicalGetData(name).ShouldBeNull();
    }

    [Fact]
    public async Task HostContext_flows_only_for_affinative_values()
    {
        CallContext.HostContext = "illogical";
        (await Task.Run(() => CallContext.HostContext)).ShouldBeNull();

        var logical = new LogicalValue();
        CallContext.HostContext = logical;

        (await Task.Run(() => CallContext.HostContext)).ShouldBeSameAs(logical);
        CallContext.HostContext = null;
    }

    [Fact]
    public void Logical_data_flows_to_new_thread_but_illogical_data_does_not()
    {
        var logicalName = Guid.NewGuid().ToString("N");
        var illogicalName = Guid.NewGuid().ToString("N");
        CallContext.LogicalSetData(logicalName, "logical");
        CallContext.SetData(illogicalName, "illogical");
        object? logical = null;
        object? illogical = null;
        var thread = new Thread(() =>
        {
            logical = CallContext.GetData(logicalName);
            illogical = CallContext.GetData(illogicalName);
        });

        thread.Start();
        thread.Join();

        logical.ShouldBe("logical");
        illogical.ShouldBeNull();
        CallContext.FreeNamedDataSlot(logicalName);
        CallContext.FreeNamedDataSlot(illogicalName);
    }

    private sealed class LogicalValue : ILogicalThreadAffinative;
}
