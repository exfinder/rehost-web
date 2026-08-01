#nullable enable

namespace System.Web.Util;

using System;

// Framework let a monitor exception reach the timer thread and end the process, which here
// discards every session and in-flight request for a failure in a subsystem that only observes.
internal sealed class MonitorFailurePolicy
{
    private const int ConsecutiveFailureLimit = 3;

    private readonly string _monitor;
    private int _consecutiveFailures;

    internal MonitorFailurePolicy(string monitor)
    {
        _monitor = monitor;
    }

    internal void RecordSuccess()
    {
        _consecutiveFailures = 0;
    }

    // True once the monitor should stop rather than keep retrying.
    internal bool RecordFailure(Exception e)
    {
        WebFormsRuntimeEventSource.Log.MonitorSampleFailed(_monitor, e.ToString());

        if (++_consecutiveFailures < ConsecutiveFailureLimit)
        {
            return false;
        }

        WebFormsRuntimeEventSource.Log.MonitorDisabled(_monitor, e.ToString());

        // Nothing subscribes to the event source by default; a container captures standard error.
        Console.Error.WriteLine(
            "Rehost.WebForms: {0} disabled after {1} consecutive failures; memory trimming is off. {2}",
            _monitor,
            ConsecutiveFailureLimit,
            e);

        return true;
    }
}
