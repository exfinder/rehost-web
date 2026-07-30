using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Compilation;

// Activating an application permanently mutates process-global state, so every scenario here runs
// in its own child process through Rehost.WebForms.ScenarioHost. The gate is port-local; see
// ADR 0044.
public sealed class PageCompilationTests
{
    private const string Request = "/Default.aspx?value=a%26c%20%22q%22%20%C3%A9";

    [Fact]
    public void Compiles_And_Renders_A_Page_On_The_First_Request()
    {
        using var application = PageApplication.Create();

        var trace = application.Run(Request);

        trace.ShouldContain("request:" + Request + ":200");
        application.ReadResponse(0).ShouldBe(application.ExpectedResponse);
    }

    // The rendered stages come from App_Code and Global.asax, so a page assembly that resolved
    // either of them into a separate load context would render different text rather than fail.
    [Fact]
    public void Renders_Values_Produced_By_App_Code_And_Global_Asax()
    {
        using var application = PageApplication.Create();

        application.Run(Request);

        application.ReadResponseText(0)
            .ShouldContain("<p id=\"stages\">app-initialize|application-start</p>");
    }

    [Fact]
    public void Reuses_The_Page_Assembly_Across_A_Restart()
    {
        using var application = PageApplication.Create();
        application.Run(Request);
        var first = application.PageAssemblies();

        application.Run(Request);

        first.ShouldNotBeEmpty();
        application.PageAssemblies().ShouldBe(first);
        application.ReadResponse(0).ShouldBe(application.ExpectedResponse);
    }

    [Fact]
    public void Recompiles_The_Page_After_The_Markup_Changes()
    {
        using var application = PageApplication.Create();
        application.Run(Request);
        var first = application.PageAssemblies();

        application.EditMarkup();
        application.Run(Request);

        application.PageAssemblies().ShouldNotBe(first);
    }

    // Ledger P39. RoslynCSharpCodeProvider reports no GeneratorSupport.Win32Resources, so
    // UseResourceLiteralString is false and a literal run past the 256-character threshold stays
    // an ordinary metadata string. The Win32 read-back path is therefore unreachable rather than
    // ported, and StringResourceManager is inactive.
    [Fact]
    public void A_Compiled_Page_Reads_No_Literal_Through_A_Win32_String_Resource()
    {
        using var application = PageApplication.Create();
        application.Run(Request);

        var assembly = application.PageAssemblyPath();
        using var stream = File.OpenRead(assembly);
        using var portableExecutable = new PEReader(stream);
        var metadata = portableExecutable.GetMetadataReader();

        var referenced = metadata.MemberReferences
            .Select(handle => metadata.GetString(metadata.GetMemberReference(handle).Name))
            .ToHashSet(StringComparer.Ordinal);

        // The literal reaches the response through the ordinary writer instead.
        referenced.ShouldContain("Write");
        referenced.ShouldNotContain("WriteUTF8ResourceString");
        referenced.ShouldNotContain("CreateResourceBasedLiteralControl");
        referenced.ShouldNotContain("SetStringResourcePointer");

        portableExecutable.PEHeaders.PEHeader!.ResourceTableDirectory.Size.ShouldBe(0);

        // Without this the assertions above would also hold for a page carrying no long literal
        // at all, which is the shape that would silently stop covering the deviation.
        File.ReadAllBytes(assembly).AsSpan()
            .IndexOf(Encoding.Unicode.GetBytes("moves markup into a Win32 string resource"))
            .ShouldBeGreaterThan(-1);
    }

    private sealed class PageApplication : IDisposable
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
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null
                && !File.Exists(Path.Combine(directory.FullName, "Rehost.WebForms.slnx")))
            {
                directory = directory.Parent;
            }

            var root = directory?.FullName
                ?? throw new InvalidOperationException("Repository root was not found.");

            return Path.Combine(
                root,
                "tests",
                "Rehost.WebForms.ScenarioHost",
                "bin",
                "Debug",
                "net10.0");
        }
    }
}
