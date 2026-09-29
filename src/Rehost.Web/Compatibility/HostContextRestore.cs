#nullable enable

namespace System.Web;

using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;

// Framework re-established HttpContext.Current inside ExecutionContext.Run through
// AspNetHostExecutionContextManager -> ThreadContext.EnterExecutionContext; the CLR hook it rode
// does not exist on modern .NET, so the compat CallContext raises its restore seam instead. The
// guard mirrors AspNetHostExecutionContextManager.SetHostExecutionContext: restore only on a
// thread whose associated ThreadContext serves the same request the wiped value was captured
// from — never on unmanaged pool threads (ConfigureAwait(false) stays null, as on Framework).
internal static class HostContextRestore
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register() =>
        CallContext.RegisterHostContextRestorer(Restore);

    private static object? Restore(object? flowed)
    {
        var threadContext = ThreadContext.Current;
        if (threadContext == null || threadContext.HasBeenDisassociatedFromThread)
        {
            return null;
        }

        var current = threadContext.HttpContext;
        return ReferenceEquals(flowed, current) ? current : null;
    }
}
