using System.Diagnostics;

namespace Rehost.WebForms.Hosting.Tests;

// Typed view over the fixture's /witness endpoint: in-memory server-side facts fetched over
// HTTP. Nothing here touches the file journal.
internal sealed class WitnessReader(ScenarioClient client)
{
    // Witness fetches often follow a request whose connection the server deliberately aborted
    // (413s, client aborts). The pooled connection's RST can race the reuse, so one transient
    // transport failure retries on a fresh connection rather than failing the assertion.
    internal async Task<string[]> EventsAsync()
    {
        string text;
        try
        {
            text = (await client.GetAsync("/witness")).Text;
        }
        catch (HttpRequestException exception) when (exception.InnerException is IOException)
        {
            text = (await client.GetAsync("/witness")).Text;
        }

        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    internal async Task<string[]> HandlerEntriesAsync() =>
        [.. (await EventsAsync()).Where(e => e.StartsWith("handler-entered:", StringComparison.Ordinal))];

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
