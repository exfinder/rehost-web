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
        RehostWebEventSource.Log.MonitorSampleFailed(_monitor, e);

        if (++_consecutiveFailures < ConsecutiveFailureLimit)
        {
            return false;
        }

        RehostWebEventSource.Log.MonitorDisabled(_monitor, e);

        return true;
    }
}
