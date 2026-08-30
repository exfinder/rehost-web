using Microsoft.Extensions.Logging;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioHost;

// Only the runtime's own category is kept: the journal answers what the adapter delivered into
// the host's log pipeline, not what Kestrel and Hosting said around it.
public static class HostLogJournal
{
    private static readonly Lock Gate = new();
    private static readonly List<string> Entries = [];

    public static string Snapshot()
    {
        lock (Gate)
        {
            return string.Join("\n", Entries);
        }
    }

    internal static void Record(HostLogEntry entry)
    {
        var line = HostLogProtocol.Format(entry);
        lock (Gate)
        {
            Entries.Add(line);
        }
    }
}

public sealed class HostLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new JournalLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class JournalLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            category == System.Web.Util.RuntimeDiagnostics.Category;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            HostLogJournal.Record(
                new HostLogEntry(
                    category,
                    logLevel.ToString(),
                    eventId.Id,
                    exception?.GetType().FullName ?? "",
                    exception?.ToString() ?? "",
                    formatter(state, exception)));
        }
    }
}
