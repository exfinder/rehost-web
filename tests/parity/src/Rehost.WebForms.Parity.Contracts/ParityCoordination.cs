using System;
using System.Collections.Generic;
using System.Threading;

namespace Rehost.WebForms.Parity.Contracts;

// The recorder opens a request's gate once ProcessRequest has returned, so an asynchronous
// handler can be made to finish strictly afterwards without sleeping. Waits time out rather
// than block forever: a runtime that completes the handler synchronously would otherwise
// deadlock against a gate that only opens after it returns.
public static class ParityGate
{
    private static readonly object Sync = new object();
    private static readonly HashSet<string> Opened = new HashSet<string>(StringComparer.Ordinal);

    public static void Open(string name)
    {
        lock (Sync)
        {
            Opened.Add(name);
            Monitor.PulseAll(Sync);
        }
    }

    public static bool Wait(string name, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        lock (Sync)
        {
            while (!Opened.Contains(name))
            {
                var remaining = deadline - DateTime.UtcNow;

                if (remaining <= TimeSpan.Zero)
                {
                    return false;
                }

                Monitor.Wait(Sync, remaining);
            }

            return true;
        }
    }
}

// Firing requests together does not make them overlap; the runtime is free to run them one
// after another. Holding every request in a step until the whole step has arrived makes the
// overlap a fact, which is what the concurrency scenarios claim to observe.
public static class ParityBarrier
{
    private static readonly object Sync = new object();
    private static int _required;
    private static int _arrived;

    public static void Begin(int required)
    {
        lock (Sync)
        {
            _required = required;
            _arrived = 0;
        }
    }

    public static bool Arrive(TimeSpan timeout)
    {
        lock (Sync)
        {
            if (_required <= 1)
            {
                return true;
            }

            _arrived++;

            if (_arrived >= _required)
            {
                Monitor.PulseAll(Sync);
                return true;
            }

            var deadline = DateTime.UtcNow + timeout;

            while (_arrived < _required)
            {
                var remaining = deadline - DateTime.UtcNow;

                if (remaining <= TimeSpan.Zero)
                {
                    return false;
                }

                Monitor.Wait(Sync, remaining);
            }

            return true;
        }
    }
}
