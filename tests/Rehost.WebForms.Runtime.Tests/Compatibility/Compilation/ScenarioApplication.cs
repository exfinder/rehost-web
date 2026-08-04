using System.Diagnostics;
using Shouldly;
using Rehost.WebForms.Parity.Harness;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;
// Activating an application permanently mutates process-global state, so every scenario driven from
// here runs in its own child process through Rehost.WebForms.ScenarioHost. The slice-2 gate is
// port-local; see ADR 0043.
internal sealed class ScenarioApplication : IDisposable
{
    private static readonly string HostDirectory = FindHostDirectory();

    private readonly DirectoryInfo _root;
    private readonly bool _ownsRoot;
    private int _runs;

    private ScenarioApplication(
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

    internal static ScenarioApplication Create()
    {
        var root = Directory.CreateTempSubdirectory("rehost-codegen-");
        var applicationPath = Path.Combine(root.FullName, "app");
        CopyDirectory(Path.Combine(HostDirectory, "fixtures", "codegen"), applicationPath);
        Directory.CreateDirectory(Path.Combine(root.FullName, "temp"));

        return new ScenarioApplication(
            root,
            applicationPath,
            Path.Combine(root.FullName, "temp"),
            ownsRoot: true);
    }

    // A second application directory pointed at the same codegen root: two processes, one
    // segment is impossible from one directory because the segment is derived from it.
    internal ScenarioApplication CloneApplicationSharingCodegenRoot()
    {
        var root = Directory.CreateTempSubdirectory("rehost-codegen-peer-");
        return new ScenarioApplication(root, ApplicationPath, CodegenRoot, ownsRoot: true);
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
        var process = StartRun(holdGate: null, requests);
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, process.StandardError);

        return ReadTrace(TracePath);
    }

    internal ScenarioProcess StartRun(string? holdGate, params string[] requests)
    {
        _runs++;
        if (File.Exists(TracePath))
        {
            File.Delete(TracePath);
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = HostDirectory,
        };
        startInfo.ArgumentList.Add(Path.Combine(HostDirectory, "Rehost.WebForms.ScenarioHost.dll"));
        startInfo.ArgumentList.Add("--app");
        startInfo.ArgumentList.Add(ApplicationPath);
        startInfo.ArgumentList.Add("--temp");
        startInfo.ArgumentList.Add(CodegenRoot);
        startInfo.ArgumentList.Add("--trace");
        startInfo.ArgumentList.Add(TracePath);
        if (holdGate != null)
        {
            startInfo.ArgumentList.Add("--hold-gate");
            startInfo.ArgumentList.Add(holdGate);
        }

        foreach (var request in requests)
        {
            startInfo.ArgumentList.Add("--request");
            startInfo.ArgumentList.Add(request);
        }

        return new ScenarioProcess(Process.Start(startInfo)!);
    }

    internal static List<string> ReadTrace(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    internal static void WaitForEntry(string path, string entry, TimeSpan timeout, ScenarioProcess process)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path) && ReadTrace(path).Contains(entry))
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

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static string FindHostDirectory()
    {
        return TestOutputPaths.TestProjectOutput("Rehost.WebForms.ScenarioHost");
    }
}
