using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.TestSupport;

// One staged application on disk: a disposable root holding the fixture copy and everything the
// scenario host writes. Nothing serves the build-output fixture directory, which later builds
// overwrite and a served application may mutate.
public sealed class StagedApplication : IDisposable
{
    private readonly DirectoryInfo _root;

    private StagedApplication(DirectoryInfo root)
    {
        _root = root;
        ApplicationPath = Path.Combine(root.FullName, "app");
        CompilationTempDirectory = Path.Combine(root.FullName, "temp");
        ResponseDirectory = Path.Combine(root.FullName, "responses");
        TracePath = Path.Combine(root.FullName, "trace.txt");
    }

    public static StagedApplication Stage(string fixtureName, string? rootPath = null)
    {
        var root = rootPath == null
            ? Directory.CreateTempSubdirectory("rehost-staged-")
            : Directory.CreateDirectory(
                Path.Combine(rootPath, "staged-" + Guid.NewGuid().ToString("N")));
        var staged = new StagedApplication(root);
        TestFiles.CopyDirectory(
            ScenarioHostInvocation.FixturePath(fixtureName), staged.ApplicationPath);
        Directory.CreateDirectory(staged.CompilationTempDirectory);
        Directory.CreateDirectory(staged.ResponseDirectory);
        return staged;
    }

    public string RootPath => _root.FullName;

    public string ApplicationPath { get; }

    public string CompilationTempDirectory { get; }

    public string ResponseDirectory { get; }

    public string TracePath { get; }

    public ServeInvocation Serve() => Wire(new ServeInvocation());

    public BatchInvocation Batch() => Wire(new BatchInvocation());

    private TInvocation Wire<TInvocation>(TInvocation invocation)
        where TInvocation : ScenarioHostInvocation<TInvocation> => invocation
        .Application(ApplicationPath)
        .CompilationTemp(CompilationTempDirectory)
        .Trace(TracePath)
        .ResponseDirectory(ResponseDirectory);

    public List<string> Trace() => TraceChannel.ReadLines(TracePath);

    public void ResetTrace()
    {
        if (File.Exists(TracePath))
        {
            File.Delete(TracePath);
        }
    }

    public byte[] Response(int index) =>
        File.ReadAllBytes(Path.Combine(ResponseDirectory, index + ".body"));

    public string ResponseText(int index) =>
        File.ReadAllText(Path.Combine(ResponseDirectory, index + ".body"));

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
