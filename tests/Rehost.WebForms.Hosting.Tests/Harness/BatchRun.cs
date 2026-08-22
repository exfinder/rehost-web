using Shouldly;
using Rehost.WebForms.Parity.Contracts;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Hosting.Tests;

internal sealed class BatchRun : IDisposable
{
    private readonly StagedApplication _staged;

    private BatchRun(StagedApplication staged, List<string> trace)
    {
        _staged = staged;
        Trace = trace;
    }

    internal string ApplicationPath => _staged.ApplicationPath;

    private List<string> Trace { get; }

    internal byte[] Response(int index) => _staged.Response(index);

    internal string ResponseText(int index) => _staged.ResponseText(index);

    internal byte[] Response(string label, int occurrence = 0) => Response(Find(label, occurrence).Index);

    internal string ResponseText(string label, int occurrence = 0) =>
        ResponseText(Find(label, occurrence).Index);

    internal int Status(string label, int occurrence = 0) => Find(label, occurrence).Status;

    // Responses are numbered in the order they were recorded, and so are the trace's request
    // lines, so a label locates one without the caller counting positions. A label repeats when a
    // probe issues more than one request, hence the occurrence.
    private (int Index, int Status) Find(string label, int occurrence)
    {
        var seen = 0;
        var index = 0;

        foreach (var entry in Trace)
        {
            if (!entry.StartsWith(TraceEvents.Request, StringComparison.Ordinal))
            {
                continue;
            }

            // An aborted probe records a request line but no response, so it must not advance the
            // ordinal. Its status is a word rather than a code.
            var separator = entry.LastIndexOf(':');
            if (!int.TryParse(entry[(separator + 1)..], out var status))
            {
                continue;
            }

            if (entry[TraceEvents.Request.Length..separator] == label && seen++ == occurrence)
            {
                return (index, status);
            }

            index++;
        }

        throw new InvalidOperationException(
            $"The trace has no request '{label}' at occurrence {occurrence}: "
                + string.Join(", ", Trace));
    }

    // Responses are numbered in order across probes: one render, then one per postback round.
    internal static BatchRun Farm(params string[] probes)
    {
        var staged = StagedApplication.Stage(Fixtures.Farm.Name);

        var invocation = staged.Invocation().Serve();
        foreach (var probe in probes)
        {
            invocation.Postback(probe);
        }

        using var process = invocation.Start();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, process.StandardError);

        return new BatchRun(staged, staged.Trace());
    }

    public void Dispose() => _staged.Dispose();
}
