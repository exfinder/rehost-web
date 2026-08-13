using Shouldly;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Hosting.Tests;

internal sealed class ScenarioRun : IDisposable
{
    private readonly DirectoryInfo _root;

    private ScenarioRun(DirectoryInfo root, string applicationPath, List<string> trace)
    {
        _root = root;
        ApplicationPath = applicationPath;
        Trace = trace;
    }

    internal string ApplicationPath { get; }

    internal List<string> Trace { get; }

    internal byte[] Response(int index) =>
        File.ReadAllBytes(Path.Combine(_root.FullName, "responses", index + ".body"));

    internal string ResponseText(int index) =>
        File.ReadAllText(Path.Combine(_root.FullName, "responses", index + ".body"));

    internal byte[] Response(string label, int occurrence = 0) => Response(IndexOf(label, occurrence));

    internal string ResponseText(string label, int occurrence = 0) =>
        ResponseText(IndexOf(label, occurrence));

    // Responses are numbered in the order they were recorded, and so are the trace's request
    // lines, so a label locates one without the caller counting positions. A label repeats when a
    // probe issues more than one request, hence the occurrence.
    private int IndexOf(string label, int occurrence)
    {
        var seen = 0;
        var index = 0;

        foreach (var entry in Trace)
        {
            if (!entry.StartsWith("request:", StringComparison.Ordinal))
            {
                continue;
            }

            // An aborted probe records a request line but no response, so it must not advance the
            // ordinal. Its status is a word rather than a code.
            var status = entry.LastIndexOf(':');
            if (!int.TryParse(entry[(status + 1)..], out _))
            {
                continue;
            }

            if (entry[8..status] == label && seen++ == occurrence)
            {
                return index;
            }

            index++;
        }

        throw new InvalidOperationException(
            $"The trace has no request '{label}' at occurrence {occurrence}: "
                + string.Join(", ", Trace));
    }

    internal ScenarioJournalReader Journal => ScenarioJournalReader.Parse(Trace);

    // Responses are numbered in order across probes: one render, then one per postback round.
    internal static ScenarioRun Farm(params string[] probes)
    {
        var root = Directory.CreateTempSubdirectory("rehost-page-kestrel-");
        var applicationPath = Path.Combine(root.FullName, "app");
        var responses = Path.Combine(root.FullName, "responses");
        var temp = Path.Combine(root.FullName, "temp");
        var tracePath = Path.Combine(root.FullName, "trace.txt");

        TestFiles.CopyDirectory(
            ScenarioHostInvocation.FixturePath(Fixtures.Farm.Name),
            applicationPath);
        Directory.CreateDirectory(responses);
        Directory.CreateDirectory(temp);

        var invocation = new ScenarioHostInvocation()
            .Serve()
            .Application(applicationPath)
            .CompilationTemp(temp)
            .Trace(tracePath)
            .ResponseDirectory(responses);
        foreach (var probe in probes)
        {
            invocation.Postback(probe);
        }

        using var process = invocation.Start();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, process.StandardError);

        return new ScenarioRun(root, applicationPath, TraceFile.ReadLines(tracePath));
    }

    public void Dispose()
    {
        try
        {
            _root.Delete(recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

}
