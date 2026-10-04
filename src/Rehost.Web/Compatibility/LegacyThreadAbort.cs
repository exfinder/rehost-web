#nullable enable

using System.Runtime.CompilerServices;
using System.Threading;

namespace System.Web;

internal static class LegacyThreadAbort
{
    private const int CorEThreadAborted = unchecked((int)0x80131530);

    private static readonly object ExceptionStateKey = new();

    internal static ThreadAbortException Create(bool timeout)
    {
        var abort = (ThreadAbortException)RuntimeHelpers.GetUninitializedObject(typeof(ThreadAbortException));
        MessageField(abort) = SR.GetString(timeout ? SR.Request_timed_out : SR.Request_terminated_by_response_end);
        HResultField(abort) = CorEThreadAborted;
        abort.SetLegacyExceptionState(new HttpApplication.CancelModuleException(timeout));
        return abort;
    }

    internal static void SetLegacyExceptionState(
        this ThreadAbortException abort, HttpApplication.CancelModuleException state) =>
        abort.Data[ExceptionStateKey] = state;

    internal static HttpApplication.CancelModuleException? GetLegacyExceptionState(this ThreadAbortException abort) =>
        abort.Data[ExceptionStateKey] as HttpApplication.CancelModuleException;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_message")]
    private static extern ref string? MessageField(Exception exception);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_HResult")]
    private static extern ref int HResultField(Exception exception);
}
