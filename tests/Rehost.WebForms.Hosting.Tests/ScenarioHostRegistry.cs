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

    internal LiveScenario GetOrAdd(ScenarioFixture fixture) =>
        _hosts.GetOrAdd(
            fixture.Name,
            _ => new Lazy<LiveScenario>(
                () => new LiveScenario(fixture),
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
