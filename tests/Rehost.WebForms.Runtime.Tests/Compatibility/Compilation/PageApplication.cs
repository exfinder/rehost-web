using System.Diagnostics;
using Shouldly;
using Rehost.WebForms.Parity.Harness;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

// Activating an application permanently mutates process-global state, so every scenario driven from
// here runs in its own child process through Rehost.WebForms.ScenarioHost. The gate is port-local;
// see ADR 0044.
internal sealed class PageApplication : IDisposable
{
    private static readonly string HostDirectory = FindHostDirectory();

    private readonly DirectoryInfo _root;
    private int _runs;

    private PageApplication(DirectoryInfo root)
    {
        _root = root;
        ApplicationPath = Path.Combine(root.FullName, "app");
        CodegenRoot = Path.Combine(root.FullName, "temp");
        ResponseDirectory = Path.Combine(root.FullName, "responses");
    }

    internal string ApplicationPath { get; }

    internal string CodegenRoot { get; }

    internal string ResponseDirectory { get; }

    internal string TracePath => Path.Combine(_root.FullName, "trace.txt");

    internal byte[] ExpectedResponse =>
        File.ReadAllBytes(Path.Combine(ApplicationPath, "Default.expected.html"));

    internal static PageApplication Create()
    {
        var root = Directory.CreateTempSubdirectory("rehost-page-");
        var application = new PageApplication(root);

        CopyDirectory(
            Path.Combine(HostDirectory, "fixtures", "page"),
            application.ApplicationPath);
        Directory.CreateDirectory(application.CodegenRoot);
        Directory.CreateDirectory(application.ResponseDirectory);

        return application;
    }

    internal byte[] ReadResponse(int index) =>
        File.ReadAllBytes(Path.Combine(ResponseDirectory, index + ".body"));

    internal string ReadResponseText(int index) =>
        File.ReadAllText(Path.Combine(ResponseDirectory, index + ".body"));

    // Generated page assemblies carry a random suffix, so identity is the file name rather
    // than a timestamp: a recompile produces a differently named assembly.
    internal string[] PageAssemblies()
    {
        var segment = Directory.GetDirectories(Path.Combine(CodegenRoot, "root")).Single();

        return Directory.GetFiles(segment, "App_Web_*.dll")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order()
            .ToArray();
    }

    internal string PageAssemblyPath()
    {
        var segment = Directory.GetDirectories(Path.Combine(CodegenRoot, "root")).Single();

        return Directory.GetFiles(segment, "App_Web_*.dll").Single();
    }

    internal void EditMarkup()
    {
        var path = Path.Combine(ApplicationPath, "Default.aspx");
        File.AppendAllText(path, "<!-- edited -->" + Environment.NewLine);
        // The top-level hash is built from timestamps, whose resolution the edit can outrun.
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(_runs + 1));
    }

    internal List<string> Run(params string[] requests)
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
        startInfo.ArgumentList.Add("--response-dir");
        startInfo.ArgumentList.Add(ResponseDirectory);

        foreach (var request in requests)
        {
            startInfo.ArgumentList.Add("--request");
            startInfo.ArgumentList.Add(request);
        }

        using var process = Process.Start(startInfo)!;
        var standardError = process.StandardError.ReadToEndAsync();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        Task.WaitAll(standardError, standardOutput);
        process.WaitForExit();

        process.ExitCode.ShouldBe(0, standardError.Result);

        return File.ReadAllLines(TracePath).ToList();
    }

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

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static string FindHostDirectory()
    {
        return TestOutputPaths.TestProjectOutput("Rehost.WebForms.ScenarioHost");
    }
}
