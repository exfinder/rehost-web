namespace Rehost.WebForms.Hosting.Tests;

// The only place that knows the trace-line grammar. Tests assert on the typed views; raw trace
// strings never appear in test bodies.
internal sealed class ScenarioJournalReader
{
    internal sealed record RequestEntry(string Label, string Status);

    internal sealed record HeaderEcho(string Name, string Value);

    internal sealed record AbortLatency(bool Apm, long Milliseconds);

    private ScenarioJournalReader()
    {
    }

    internal string? Address { get; private set; }

    internal IReadOnlyList<RequestEntry> Requests => _requests;

    internal IReadOnlyList<string> HandlerEntries => _handlerEntries;

    internal IReadOnlyList<string> ContentTypes => _contentTypes;

    internal IReadOnlyList<string> ErrorBodies => _errorBodies;

    internal IReadOnlyList<HeaderEcho> HeaderEchoes => _headerEchoes;

    internal IReadOnlyList<AbortLatency> AbortLatencies => _abortLatencies;

    internal IReadOnlyList<string> Unknown => _unknown;

    private readonly List<RequestEntry> _requests = [];
    private readonly List<string> _handlerEntries = [];
    private readonly List<string> _contentTypes = [];
    private readonly List<string> _errorBodies = [];
    private readonly List<HeaderEcho> _headerEchoes = [];
    private readonly List<AbortLatency> _abortLatencies = [];
    private readonly List<string> _unknown = [];

    internal static ScenarioJournalReader Parse(IEnumerable<string> lines)
    {
        var journal = new ScenarioJournalReader();

        foreach (var line in lines)
        {
            if (line.StartsWith("address:", StringComparison.Ordinal))
            {
                journal.Address = line["address:".Length..];
            }
            else if (line.StartsWith("request:", StringComparison.Ordinal))
            {
                var status = line.LastIndexOf(':');
                journal._requests.Add(new RequestEntry(
                    line["request:".Length..status],
                    line[(status + 1)..]));
            }
            else if (line.StartsWith("handler-entered:", StringComparison.Ordinal))
            {
                journal._handlerEntries.Add(line["handler-entered:".Length..]);
            }
            else if (line.StartsWith("content-type:", StringComparison.Ordinal))
            {
                journal._contentTypes.Add(line["content-type:".Length..]);
            }
            else if (line.StartsWith("error-body:", StringComparison.Ordinal))
            {
                journal._errorBodies.Add(line["error-body:".Length..]);
            }
            else if (line.StartsWith("x-", StringComparison.Ordinal)
                && line.IndexOf(':') is > 0 and var separator)
            {
                journal._headerEchoes.Add(new HeaderEcho(
                    line[..separator],
                    line[(separator + 1)..]));
            }
            else if (line.StartsWith("body-apm-abort-latency-ms:", StringComparison.Ordinal))
            {
                journal._abortLatencies.Add(new AbortLatency(
                    true,
                    long.Parse(line["body-apm-abort-latency-ms:".Length..])));
            }
            else if (line.StartsWith("body-abort-latency-ms:", StringComparison.Ordinal))
            {
                journal._abortLatencies.Add(new AbortLatency(
                    false,
                    long.Parse(line["body-abort-latency-ms:".Length..])));
            }
            else
            {
                journal._unknown.Add(line);
            }
        }

        return journal;
    }
}
