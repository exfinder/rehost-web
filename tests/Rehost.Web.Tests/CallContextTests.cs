using Shouldly;
using System.Runtime.Remoting.Messaging;
using CallContext = System.Web.Util.LegacyCallContext;
using Xunit;

namespace Rehost.Web.Tests;

public sealed class CallContextTests
{
    [Fact]
    public void Illogical_Data_Does_Not_Flow_To_New_Thread()
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
    public void Parent_Illogical_Data_Is_Restored_After_Child_Execution_Context()
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
    public async Task Logical_Data_Flows_Into_Task_Without_Child_Writes_Leaking_Back()
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
    public async Task SetData_Uses_Logical_Storage_For_Affinative_Values()
    {
        var name = Guid.NewGuid().ToString("N");
        var value = new LogicalValue();

        CallContext.SetData(name, value);

        (await Task.Run(() => CallContext.GetData(name))).ShouldBeSameAs(value);
        CallContext.LogicalGetData(name).ShouldBeSameAs(value);
        CallContext.FreeNamedDataSlot(name);
    }

    [Fact]
    public void FreeNamedDataSlot_Clears_Logical_And_Illogical_Storage()
    {
        var name = Guid.NewGuid().ToString("N");
        CallContext.LogicalSetData(name, "logical");
        CallContext.SetData(name, "illogical");

        CallContext.FreeNamedDataSlot(name);

        CallContext.GetData(name).ShouldBeNull();
        CallContext.LogicalGetData(name).ShouldBeNull();
    }

    [Fact]
    public async Task HostContext_Flows_Only_For_Affinative_Values()
    {
        CallContext.HostContext = "illogical";
        object? illogical = null;
        // A pooled thread can both suspend and resume the same illogical state, which
        // OnIllogicalContextChanged then reads as a resumption rather than inheritance. Only a
        // thread that never suspended it observes non-inheritance deterministically.
        var thread = new Thread(() => illogical = CallContext.HostContext);

        thread.Start();
        thread.Join();

        illogical.ShouldBeNull();

        var logical = new LogicalValue();
        CallContext.HostContext = logical;

        (await Task.Run(() => CallContext.HostContext)).ShouldBeSameAs(logical);
        CallContext.HostContext = null;
    }

    [Fact]
    public void Logical_Data_Flows_To_New_Thread_But_Illogical_Data_Does_Not()
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

    [Fact]
    public void A_Second_HostContext_Restorer_Registration_Is_Refused()
    {
        // System.Web's module initializer holds the first registration, as Framework's
        // AppDomainManager held its one HostExecutionContextManager from AppDomain creation.
        var exception = Should.Throw<InvalidOperationException>(
            () => CallContext.RegisterHostContextRestorer(_ => null));

        exception.Message.ShouldContain("already registered");
    }

    private sealed class LogicalValue : ILogicalThreadAffinative;
}
