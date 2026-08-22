using System.Diagnostics;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.Hosting.Tests;

// Typed view over the fixture's /witness endpoint: in-memory server-side facts fetched over
// HTTP. Nothing here touches the trace file. Every reader except StagesAsync sees every request
// the process served; the Scenarios.cs marker bases decide which fixtures may expose that.
internal sealed class HostWitness(ScenarioClient client)
{
    internal async Task<string[]> EventsAsync() =>
        (await client.GetAsync(ProbePaths.Witness)).Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    internal async Task<string[]> HandlerEntriesAsync() =>
        [.. (await EventsAsync()).Where(e => e.StartsWith(WitnessProtocol.HandlerEntered, StringComparison.Ordinal))];

    internal async Task<string[]> StagesAsync(string token)
    {
        var prefix = WitnessProtocol.StagePrefix + token + ":";
        return [.. (await EventsAsync())
            .Where(e => e.StartsWith(prefix, StringComparison.Ordinal))
            .Select(e => e[prefix.Length..])];
    }

    internal async Task<string> WaitForAsync(string prefix, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();

        while (deadline.Elapsed < timeout)
        {
            var match = (await EventsAsync()).FirstOrDefault(
                e => e.StartsWith(prefix, StringComparison.Ordinal));
            if (match != null)
            {
                return match;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException(
            "No witness event with prefix '" + prefix + "' within "
            + timeout.TotalSeconds + "s. Events: "
            + string.Join(" | ", await EventsAsync()));
    }
}
