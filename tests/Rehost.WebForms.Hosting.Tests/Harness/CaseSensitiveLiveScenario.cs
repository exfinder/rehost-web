using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Hosting.Tests;

// The page fixture served from a genuinely case-sensitive filesystem, where /default.aspx and
// Default.aspx are different names unless the port folds them. Live is null where the platform
// cannot provide such a filesystem (Windows/NTFS); tests skip.
public sealed class CaseSensitiveLiveScenario : IDisposable
{
    private readonly CaseSensitiveDirectory _volume = CaseSensitiveDirectory.Create();

    public CaseSensitiveLiveScenario()
    {
        if (_volume.Path != null)
        {
            Live = new LiveScenario(Fixtures.Page, _volume.Path);
        }
    }

    internal LiveScenario? Live { get; }

    public void Dispose()
    {
        Live?.Dispose();
        _volume.Dispose();
    }
}
