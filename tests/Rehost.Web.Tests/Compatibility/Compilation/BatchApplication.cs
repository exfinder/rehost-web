using Rehost.Web.ScenarioProtocol;
using Shouldly;
using Rehost.Web.TestSupport;

namespace Rehost.Web.Tests.Compatibility.Compilation;

// Activating an application permanently mutates process-global state, so every scenario driven from
// here runs in its own child process through Rehost.Web.ScenarioHost. The slice-2 gate is
// port-local; see ADR 0043.
internal sealed class BatchApplication : IDisposable
{
    private readonly IDisposable _owner;
    private int _runs;

    private BatchApplication(
        IDisposable owner,
        string applicationPath,
        string codegenRoot,
        string tracePath,
        string responseDirectory)
    {
        _owner = owner;
        ApplicationPath = applicationPath;
        CodegenRoot = codegenRoot;
        TracePath = tracePath;
        ResponseDirectory = responseDirectory;
    }

    internal string ApplicationPath { get; }

    internal string CodegenRoot { get; }

    internal string TracePath { get; }

    private string ResponseDirectory { get; }

    // One segment per application directory, named for a digest of that directory.
    internal string Segment => Directory.GetDirectories(
        Path.Combine(CodegenRoot, "root")).Single();

    internal static BatchApplication Create()
    {
        var staged = StagedApplication.Stage("codegen");
        return new BatchApplication(
            staged,
            staged.ApplicationPath,
            staged.CompilationTempDirectory,
            staged.TracePath,
            staged.ResponseDirectory);
    }

    // A second application directory pointed at the same codegen root: two processes, one
    // segment is impossible from one directory because the segment is derived from it.
    internal BatchApplication CloneApplicationSharingCodegenRoot()
    {
        var root = new TempDirectory("rehost-codegen-peer-");
        return new BatchApplication(
            root,
            ApplicationPath,
            CodegenRoot,
            root.Path("trace.txt"),
            root.Path("responses"));
    }

    internal void EditAppCode()
    {
        var path = Path.Combine(ApplicationPath, "App_Code", "Probe.cs");
        File.AppendAllText(path, Environment.NewLine + "// edited" + Environment.NewLine);
        // The top-level hash is built from timestamps, whose resolution the edit can outrun.
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(_runs + 1));
    }

    internal void AddAppCode(string fileName, string source)
    {
        File.WriteAllText(Path.Combine(ApplicationPath, "App_Code", fileName), source);
    }

    // The full body of the run's Nth request, where the trace keeps a summary.
    internal string ResponseBody(int index) =>
        File.ReadAllText(Path.Combine(ResponseDirectory, index + ".body"));

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

        Directory.CreateDirectory(ResponseDirectory);
        var invocation = new BatchInvocation()
            .Application(ApplicationPath)
            .CompilationTemp(CodegenRoot)
            .Trace(TracePath)
            .ResponseDirectory(ResponseDirectory);
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

    public void Dispose() => _owner.Dispose();
}
