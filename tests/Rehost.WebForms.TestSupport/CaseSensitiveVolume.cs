namespace Rehost.WebForms.TestSupport;

public sealed class CaseSensitiveVolume : IDisposable
{
    private readonly CaseSensitiveDirectory _directory = CaseSensitiveDirectory.Create();

    public string RequirePath() => _directory.RequirePath();

    public void Dispose() => _directory.Dispose();
}
