using Rehost.WebForms.Parity.Contracts;
using Shouldly;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;
// Activating an application permanently mutates process-global state, so every scenario driven from
// here runs in its own child process through Rehost.WebForms.ScenarioHost. The slice-2 gate is
// port-local; see ADR 0043.
internal sealed class BatchApplication : IDisposable
{
    private readonly DirectoryInfo _root;
    private readonly bool _ownsRoot;
    private int _runs;

    private BatchApplication(
        DirectoryInfo root,
        string applicationPath,
        string codegenRoot,
        bool ownsRoot)
    {
        _root = root;
        _ownsRoot = ownsRoot;
        ApplicationPath = applicationPath;
        CodegenRoot = codegenRoot;
    }

    internal string ApplicationPath { get; }

    internal string CodegenRoot { get; }

    internal string TracePath => Path.Combine(_root.FullName, "trace.txt");

    // One segment per application directory, named for a digest of that directory.
    internal string Segment => Directory.GetDirectories(
        Path.Combine(CodegenRoot, "root")).Single();

    internal static BatchApplication Create()
    {
        var root = Directory.CreateTempSubdirectory("rehost-codegen-");
        var applicationPath = Path.Combine(root.FullName, "app");
        TestFiles.CopyDirectory(ScenarioHostInvocation.FixturePath("codegen"), applicationPath);
        Directory.CreateDirectory(Path.Combine(root.FullName, "temp"));

        return new BatchApplication(
            root,
            applicationPath,
            Path.Combine(root.FullName, "temp"),
            ownsRoot: true);
    }

    // A second application directory pointed at the same codegen root: two processes, one
    // segment is impossible from one directory because the segment is derived from it.
    internal BatchApplication CloneApplicationSharingCodegenRoot()
    {
        var root = Directory.CreateTempSubdirectory("rehost-codegen-peer-");
        return new BatchApplication(root, ApplicationPath, CodegenRoot, ownsRoot: true);
    }

    internal void EditAppCode()
    {
        var path = Path.Combine(ApplicationPath, "App_Code", "Probe.cs");
        File.AppendAllText(path, Environment.NewLine + "// edited" + Environment.NewLine);
        // The top-level hash is built from timestamps, whose resolution the edit can outrun.
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(_runs + 1));
    }

    internal void BreakAppCode()
    {
        File.AppendAllText(
            Path.Combine(ApplicationPath, "App_Code", "Probe.cs"),
            Environment.NewLine + "public class Broken { this is not csharp }" + Environment.NewLine);
    }

    internal List<string> Run(params string[] requests)
    {
        using var process = StartRun(holdGate: null, requests);
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, process.StandardError);

        return TraceChannel.ReadLines(TracePath);
    }

    internal ScenarioHostProcess StartRun(string? holdGate, params string[] requests)
    {
        _runs++;
        if (File.Exists(TracePath))
        {
            File.Delete(TracePath);
        }

        var invocation = new ScenarioHostInvocation()
            .Application(ApplicationPath)
            .CompilationTemp(CodegenRoot)
            .Trace(TracePath);
        if (holdGate != null)
        {
            invocation.HoldGate(holdGate);
        }

        foreach (var request in requests)
        {
            invocation.Request(request);
        }

        return invocation.Start();
    }

    internal static void WaitForEntry(
        string path,
        string entry,
        TimeSpan timeout,
        ScenarioHostProcess process)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (TraceChannel.ReadLines(path).Contains(entry))
            {
                return;
            }

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Process exited with code {process.ExitCode} before '{entry}' appeared in {path}.{Environment.NewLine}{process.StandardError}");
            }

            Thread.Sleep(50);
        }

        throw new TimeoutException($"'{entry}' never appeared in {path}.");
    }

    public void Dispose()
    {
        if (!_ownsRoot)
        {
            return;
        }

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
