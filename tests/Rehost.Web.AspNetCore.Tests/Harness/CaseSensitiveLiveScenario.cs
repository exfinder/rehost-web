using Rehost.Web.TestSupport;
using Xunit;

[assembly: AssemblyFixture(typeof(Rehost.Web.AspNetCore.Tests.CaseSensitiveLiveScenario))]

namespace Rehost.Web.AspNetCore.Tests;

// The page fixture served from a genuinely case-sensitive filesystem, where /default.aspx and
// Default.aspx are different names unless the port folds them. RequireLive skips where the
// platform cannot provide such a filesystem (Windows/NTFS). Assembly-lifetime and lazy for the
// same reason as ScenarioHostRegistry: one volume and one host across all consuming classes,
// and none at all in a filtered run that reaches no case-sensitive test.
public sealed class CaseSensitiveLiveScenario : IDisposable
{
    private readonly Lazy<(CaseSensitiveDirectory Volume, LiveScenario? Live)> _host = new(
        () =>
        {
            var volume = CaseSensitiveDirectory.Create();
            return (volume, volume.Path == null
                ? null
                : LiveScenario.StartIsolated(
                    Fixtures.Page, IsolationReason.HostConfiguration, volume.Path));
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    internal LiveScenario RequireLive()
    {
        var (volume, live) = _host.Value;
        volume.RequirePath();
        return live!;
    }

    public void Dispose()
    {
        if (!_host.IsValueCreated)
        {
            return;
        }

        _host.Value.Live?.Dispose();
        _host.Value.Volume.Dispose();
    }
}
