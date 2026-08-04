using Rehost.WebForms.Parity.Contracts;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The journal is polled by a reader while other components append. On Windows an overlapping
// open with a narrower share mode throws, and a write that throws loses the entry — the probe's
// outcome marker — which reads back as a detection timeout (the lost-reset investigation).
public sealed class TraceJournalTests
{
    [Fact]
    public async Task Concurrent_Reads_Never_Fail_Or_Lose_A_Write()
    {
        var root = Directory.CreateTempSubdirectory("trace-journal-");
        var path = Path.Combine(root.FullName, "trace.txt");
        var original = Environment.GetEnvironmentVariable(TraceJournal.TraceVariable);
        Environment.SetEnvironmentVariable(TraceJournal.TraceVariable, path);

        try
        {
            const int Writes = 2000;
            var stop = 0;

            var reader = Task.Run(
                () =>
                {
                    while (Volatile.Read(ref stop) == 0)
                    {
                        TraceJournal.ReadAll(path);
                    }
                },
                TestContext.Current.CancellationToken);

            for (var i = 0; i < Writes; i++)
            {
                TraceJournal.Record("entry:" + i);
            }

            Volatile.Write(ref stop, 1);
            await reader;

            var lines = TraceJournal.ReadAll(path)
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            lines.Length.ShouldBe(Writes);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TraceJournal.TraceVariable, original);
            root.Delete(recursive: true);
        }
    }
}
