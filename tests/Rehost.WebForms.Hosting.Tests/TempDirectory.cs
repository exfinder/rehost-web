namespace Rehost.WebForms.Hosting.Tests;

// The application writes into a directory the test owns and deletes, never into the checkout or
// the fixture copy the host is serving.
internal sealed class TempDirectory : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-saveas-");

    internal string Path(string name) => System.IO.Path.Combine(_root.FullName, name);

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
