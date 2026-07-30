using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

// Activating an application permanently mutates process-global state, so every scenario here runs
// in its own child process through Rehost.WebForms.ScenarioHost. The slice-2 gate is port-local;
// see ADR 0043.
public sealed class CodegenSubstrateTests
{
    [Fact]
    public void Compiles_The_Top_Level_Files_In_Order_Before_The_First_Request()
    {
        using var application = ScenarioApplication.Create();

        var trace = application.Run();

        // Pre-application start runs from a bin assembly before anything is generated; App_Code's
        // AppInitialize runs before Application_Start, which is the first thing that can observe a
        // compiled Global.asax.
        trace.IndexOf("pre-start").ShouldBeLessThan(trace.IndexOf("app-initialize"));
        trace.IndexOf("app-initialize")
            .ShouldBeLessThan(trace.FindIndex(entry => entry.StartsWith("application-start:")));
        trace.FindIndex(entry => entry.StartsWith("application-start:"))
            .ShouldBeLessThan(trace.IndexOf("begin-request"));
        trace.IndexOf("begin-request").ShouldBeLessThan(trace.IndexOf("handler"));
        trace.ShouldContain("request:/default:200");
    }

    [Fact]
    public void Compiles_App_Code_Its_Subdirectory_And_Global_Resources()
    {
        using var application = ScenarioApplication.Create();

        var trace = application.Run();

        // Global.asax used a type from App_Code, which used one from the sub-directory assembly
        // and the generated resource class. Separate load contexts would fail this, not the
        // assembly names.
        Value(trace, "app-code:").ShouldStartWith("App_Code.");
        Value(trace, "sub-code:").ShouldStartWith("App_SubCode_Shared.");
        Value(trace, "resource:").ShouldBe("neutral-greeting");

        var segment = application.Segment;
        Directory.EnumerateFiles(segment, "App_Code.*.dll").ShouldHaveSingleItem();
        Directory.EnumerateFiles(segment, "App_SubCode_Shared.*.dll").ShouldHaveSingleItem();
        Directory.EnumerateFiles(segment, "App_GlobalResources.*.dll").ShouldHaveSingleItem();
        Directory
            .EnumerateFiles(Path.Combine(segment, "fr"), "*.resources.dll")
            .ShouldHaveSingleItem();
    }

    [Fact]
    public void Reuses_The_Previous_Run_Output_When_Nothing_Changed()
    {
        using var application = ScenarioApplication.Create();

        var first = application.Run();
        var firstAssemblies = Directory.GetFiles(application.Segment, "*.dll").Order().ToArray();

        var second = application.Run();

        // A recompile draws a new random assembly name, so identical names mean the second run
        // loaded the first run's output instead of rebuilding it.
        Value(second, "app-code:").ShouldBe(Value(first, "app-code:"));
        Value(second, "sub-code:").ShouldBe(Value(first, "sub-code:"));
        Directory.GetFiles(application.Segment, "*.dll").Order().ShouldBe(firstAssemblies);
    }

    [Fact]
    public void Recompiles_After_Application_Code_Changes()
    {
        using var application = ScenarioApplication.Create();

        var first = application.Run();

        application.EditAppCode();
        var second = application.Run();

        Value(second, "app-code:").ShouldNotBe(Value(first, "app-code:"));
        Value(second, "resource:").ShouldBe("neutral-greeting");
        Directory.EnumerateFiles(application.Segment, "App_Code.*.dll")
            .Select(Path.GetFileName)
            .ShouldNotContain(Path.GetFileName(Value(first, "app-code:")) + ".dll");
    }

    [Fact]
    public void Serves_A_Second_Process_Sharing_One_Codegen_Segment()
    {
        using var first = ScenarioApplication.Create();
        using var second = first.CloneApplicationSharingCodegenRoot();

        // Both processes compile the same application from one segment at once, which is the
        // condition the cross-process compilation mutex exists for.
        var firstRun = first.StartRun(holdMilliseconds: 4000);
        var secondTrace = second.Run();

        firstRun.WaitForExit();
        firstRun.ExitCode.ShouldBe(0, firstRun.StandardError);

        secondTrace.ShouldContain("request:/default:200");
        ScenarioApplication.ReadTrace(first.TracePath).ShouldContain("request:/default:200");
    }

    [Fact]
    public void Reclaims_An_Invalidated_Assembly_As_The_Platform_Allows()
    {
        using var application = ScenarioApplication.Create();
        application.Run();
        var stale = Directory.GetFiles(application.Segment, "App_Code.*.dll").Single();

        // The first process keeps its generated assemblies loaded while the second invalidates
        // them, which is the only way to reach the branch that cannot delete a file.
        var holding = application.StartRun(holdMilliseconds: 6000);
        ScenarioApplication.WaitForEntry(application.TracePath, "holding", TimeSpan.FromSeconds(30));

        using var editor = application.CloneApplicationSharingCodegenRoot();
        editor.EditAppCode();
        editor.Run();

        holding.WaitForExit();
        holding.ExitCode.ShouldBe(0, holding.StandardError);

        if (OperatingSystem.IsWindows())
        {
            // The loaded file is locked, so the cache leaves a marker for a later run to sweep.
            File.Exists(stale + ".delete").ShouldBeTrue();
        }
        else
        {
            // Unix unlinks a loaded file, so the marker path is never entered.
            File.Exists(stale).ShouldBeFalse();
            File.Exists(stale + ".delete").ShouldBeFalse();
        }
    }

    [Fact]
    public void Reports_A_Compile_Error_On_Every_Request_Without_Failing_Activation()
    {
        using var application = ScenarioApplication.Create();
        application.BreakAppCode();

        // Framework stashes an initialization failure and renders it per request rather than
        // aborting activation, which is what CreateObject's throwOnError:false selects. Aborting
        // instead would take the diagnostics away from whoever asked for the page.
        var trace = application.Run("/default", "/default");

        trace.FindAll(entry => entry == "request:/default:500").Count.ShouldBe(2);
        var diagnostics = trace.FindAll(entry => entry.StartsWith("error-body:"));
        diagnostics.Count.ShouldBe(2);
        diagnostics[0].ShouldContain("Compilation Error");
        diagnostics[1].ShouldBe(diagnostics[0]);
    }

    private static string Value(List<string> trace, string prefix) =>
        trace.Find(entry => entry.StartsWith(prefix))?[prefix.Length..]
            ?? throw new InvalidOperationException(
                $"No '{prefix}' entry in trace:{Environment.NewLine}{string.Join(Environment.NewLine, trace)}");

    private sealed class ScenarioApplication : IDisposable
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
            var process = StartRun(holdMilliseconds: 0, requests);
            process.WaitForExit();
            process.ExitCode.ShouldBe(0, process.StandardError);

            return ReadTrace(TracePath);
        }

        internal ScenarioProcess StartRun(int holdMilliseconds, params string[] requests)
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
            if (holdMilliseconds > 0)
            {
                startInfo.ArgumentList.Add("--hold-ms");
                startInfo.ArgumentList.Add(holdMilliseconds.ToString());
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

        internal static void WaitForEntry(string path, string entry, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(path) && ReadTrace(path).Contains(entry))
                {
                    return;
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
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null
                && !File.Exists(Path.Combine(directory.FullName, "Rehost.WebForms.slnx")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new InvalidOperationException("Repository root was not found.");
            }

            // .../bin/<configuration>/net10.0/
            var configuration = Path.GetFileName(
                Path.GetDirectoryName(
                    Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)))!;

            return Path.Combine(
                directory.FullName,
                "tests",
                "Rehost.WebForms.ScenarioHost",
                "bin",
                configuration,
                "net10.0");
        }
    }

    private sealed class ScenarioProcess(Process process)
    {
        private readonly Task<string> _standardOutput = process.StandardOutput.ReadToEndAsync();
        private readonly Task<string> _standardError = process.StandardError.ReadToEndAsync();

        internal int ExitCode => process.ExitCode;

        internal string StandardError => _standardError.Result;

        internal void WaitForExit()
        {
            // Both streams must drain concurrently, or a child writing more than the pipe buffer
            // blocks while a sequential reader waits on the stream it is not draining.
            Task.WaitAll(_standardOutput, _standardError);
            process.WaitForExit();
        }
    }
}
