using Shouldly;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

// Activating an application permanently mutates process-global state, so every scenario driven from
// here runs in its own child process through Rehost.WebForms.ScenarioHost. The gate is port-local;
// see ADR 0044.
internal sealed class PageApplication : IDisposable
{
    private readonly StagedApplication _staged;
    private int _runs;

    private PageApplication(StagedApplication staged) => _staged = staged;

    internal string ApplicationPath => _staged.ApplicationPath;

    internal string CodegenRoot => _staged.CompilationTempDirectory;

    internal string TracePath => _staged.TracePath;

    internal byte[] ExpectedResponse =>
        File.ReadAllBytes(Path.Combine(ApplicationPath, "Default.expected.html"));

    internal static PageApplication Create() =>
        new(StagedApplication.Stage("page"));

    internal byte[] ReadResponse(int index) => _staged.Response(index);

    internal string ReadResponseText(int index) => _staged.ResponseText(index);

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
        _staged.ResetTrace();

        var invocation = _staged.Invocation();
        foreach (var request in requests)
        {
            invocation.Request(request);
        }

        using var process = invocation.Start();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, process.StandardError);

        return _staged.Trace();
    }

    public void Dispose() => _staged.Dispose();
}
