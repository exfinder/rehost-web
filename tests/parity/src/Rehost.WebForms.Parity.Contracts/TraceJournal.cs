using System;
using System.IO;

namespace Rehost.WebForms.Parity.Contracts;

// Scenario events come from places that share no stream — a bin assembly before the application
// starts, generated App_Code and Global.asax, and the host process itself — so they meet in one
// append-only file named by the environment.
public static class TraceJournal
{
    public const string TraceVariable = "REHOST_SCENARIO_TRACE";

    private static readonly object Gate = new object();

    public static void Record(string entry)
    {
        var path = Environment.GetEnvironmentVariable(TraceVariable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        lock (Gate)
        {
            File.AppendAllText(path, entry + Environment.NewLine);
        }
    }
}
