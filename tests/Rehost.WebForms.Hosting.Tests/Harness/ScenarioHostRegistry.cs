using System.Collections.Concurrent;
using Xunit;

[assembly: AssemblyFixture(typeof(Rehost.WebForms.Hosting.Tests.ScenarioHostRegistry))]

namespace Rehost.WebForms.Hosting.Tests;

// Runner-lifetime owner of hosts shared across test classes. Spawning is lazy so a filtered run
// that executes no scenario on a shared fixture starts no host; xunit disposes the registry after
// the last test, which is what keeps LiveScenario's kill-and-delete guarantees under sharing.
public sealed class ScenarioHostRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<LiveScenario>> _hosts = new();

    // A role names a deliberate second host of the same fixture (e.g. the abort host stays off
    // the main body host so socket kills never neighbor ordinary probes).
    internal LiveScenario GetOrAdd(ScenarioFixture fixture, string? role = null) =>
        _hosts.GetOrAdd(
            role == null ? fixture.Name : fixture.Name + "#" + role,
            _ => new Lazy<LiveScenario>(
                () => LiveScenario.StartPooled(fixture),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    public void Dispose()
    {
        var failures = new List<Exception>();
        foreach (var host in _hosts.Values.Where(h => h.IsValueCreated))
        {
            try
            {
                host.Value.Dispose();
            }
            catch (Exception disposal)
            {
                failures.Add(disposal);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "One or more shared scenario hosts failed to dispose.", failures);
        }
    }
}
