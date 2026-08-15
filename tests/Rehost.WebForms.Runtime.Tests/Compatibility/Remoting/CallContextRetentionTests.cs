using Shouldly;
using System.Runtime.Remoting.Messaging;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Remoting;

// Illogical state that a flow leaves behind on a pool thread must die with the flow. The suspended
// stack that reconstructs "resumption vs inheritance" once held every left-behind state strongly,
// so a server accumulated one entry per request per pool thread for the life of the process.
public sealed class CallContextRetentionTests
{
    private const int Flows = 2000;

    [Fact]
    public async Task Illogical_HostContext_Left_By_Completed_Flows_Is_Collectible()
    {
        var references = new List<WeakReference>();

        await Task.WhenAll(Enumerable.Range(0, Flows).Select(_ => Task.Run(async () =>
        {
            var context = new byte[4096];
            lock (references) references.Add(new WeakReference(context));
            CallContext.HostContext = context;
            await Task.Yield();
            await Task.Delay(1, TestContext.Current.CancellationToken).ConfigureAwait(false);
            CallContext.HostContext = null;
        }, TestContext.Current.CancellationToken))).WaitAsync(TestContext.Current.CancellationToken);
        Collect();

        // A live pool thread's current context may still hold a few; thousands means the stack.
        references.Count(r => r.IsAlive).ShouldBeLessThan(Flows / 100);
    }

    [Fact]
    public async Task Illogical_Data_Left_By_Completed_Flows_Is_Collectible()
    {
        var name = Guid.NewGuid().ToString("N");
        var references = new List<WeakReference>();

        await Task.WhenAll(Enumerable.Range(0, Flows).Select(_ => Task.Run(async () =>
        {
            var value = new byte[4096];
            lock (references) references.Add(new WeakReference(value));
            CallContext.SetData(name, value);
            await Task.Yield();
            await Task.Delay(1, TestContext.Current.CancellationToken).ConfigureAwait(false);
            CallContext.FreeNamedDataSlot(name);
        }, TestContext.Current.CancellationToken))).WaitAsync(TestContext.Current.CancellationToken);
        Collect();

        references.Count(r => r.IsAlive).ShouldBeLessThan(Flows / 100);
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
