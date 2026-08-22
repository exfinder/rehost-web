namespace Rehost.WebForms.TestSupport;

// The application writes into a directory the test owns and deletes, never into the checkout or
// the fixture copy the host is serving.
public sealed class TempDirectory(string prefix = "rehost-saveas-") : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory(prefix);

    public string Path(string name) => System.IO.Path.Combine(_root.FullName, name);

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
