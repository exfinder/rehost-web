using Microsoft.Extensions.Logging;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Diagnostics;

internal sealed record LogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    Exception? Exception,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> State);

internal sealed class CollectingLoggerFactory : ILoggerFactory
{
    private readonly List<LogEntry> _entries = [];
    private readonly Func<Exception>? _failure;
    private bool _disposed;

    internal CollectingLoggerFactory(Func<Exception>? failure = null)
    {
        _failure = failure;
    }

    internal IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_entries)
            {
                return _entries.ToArray();
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(this, categoryName);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void Add(LogEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_failure != null)
        {
            throw _failure();
        }

        lock (_entries)
        {
            _entries.Add(entry);
        }
    }

    private sealed class CollectingLogger(CollectingLoggerFactory factory, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            factory.Add(
                new LogEntry(
                    category,
                    logLevel,
                    eventId,
                    exception,
                    formatter(state, exception),
                    state as IReadOnlyList<KeyValuePair<string, object?>> ?? []));
        }
    }
}
