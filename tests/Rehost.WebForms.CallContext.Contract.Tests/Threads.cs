namespace Rehost.WebForms.CallContext.Contract.Tests;

internal static class Threads
{
    // A thread that owns its own execution context, so what it observes is not perturbed by the
    // xunit worker's state or by pool reuse.
    public static void OnDedicated(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception e) { failure = e; }
        });
        thread.Start();
        thread.Join();
        if (failure != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public static T OnDedicated<T>(Func<T> body)
    {
        T result = default!;
        OnDedicated(() => { result = body(); });
        return result;
    }

    // A pristine ExecutionContext, as a pool thread has between work items. Captured on a fresh
    // thread because .NET Framework allows a captured context to be Run only once.
    public static ExecutionContext Pristine() => OnDedicated(() => ExecutionContext.Capture()!);
}
