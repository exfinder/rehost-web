using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Activating an application permanently mutates process-global state, so the application is served
// from a child process. This is the only coverage of a dynamically compiled page crossing the
// adapter and a real socket; every other Kestrel assertion here runs precompiled handlers whose
// responses are far smaller than a rendered page.
public sealed class PageOverKestrelTests
{
    private const string Request = "/Default.aspx?value=a%26c%20%22q%22%20%C3%A9";

    [Fact]
    public void Serves_A_Dynamically_Compiled_Page_Over_Kestrel()
    {
        using var run = ScenarioRun.Serve(Request);

        run.Trace.ShouldContain("request:" + Request + ":200");
        run.Trace.ShouldContain("content-type:text/html; charset=utf-8");
        run.Response(0).ShouldBe(run.ExpectedResponse);
    }

    // The page is routed by the shipped root configuration rather than by the fixture, so this
    // also covers the *.aspx httpHandlers mapping reaching PageHandlerFactory.
    [Fact]
    public void Refuses_A_Path_Framework_Maps_To_The_Forbidden_Handler()
    {
        using var run = ScenarioRun.Serve("/Default.aspx.cs");

        run.Trace.ShouldContain("request:/Default.aspx.cs:403");
    }

    private sealed class ScenarioRun : IDisposable
    {
        private readonly DirectoryInfo _root;

        private ScenarioRun(DirectoryInfo root, string applicationPath, List<string> trace)
        {
            _root = root;
            ApplicationPath = applicationPath;
            Trace = trace;
        }

        internal string ApplicationPath { get; }

        internal List<string> Trace { get; }

        internal byte[] ExpectedResponse =>
            File.ReadAllBytes(Path.Combine(ApplicationPath, "Default.expected.html"));

        internal byte[] Response(int index) =>
            File.ReadAllBytes(Path.Combine(_root.FullName, "responses", index + ".body"));

        internal static ScenarioRun Serve(params string[] requests)
        {
            var root = Directory.CreateTempSubdirectory("rehost-page-kestrel-");
            var applicationPath = Path.Combine(root.FullName, "app");
            var responses = Path.Combine(root.FullName, "responses");
            var temp = Path.Combine(root.FullName, "temp");
            var tracePath = Path.Combine(root.FullName, "trace.txt");

            CopyDirectory(Path.Combine(HostDirectory, "fixtures", "page"), applicationPath);
            Directory.CreateDirectory(responses);
            Directory.CreateDirectory(temp);

            var hostAssembly = Path.Combine(HostDirectory, "Rehost.WebForms.ScenarioHost.dll");
            File.Exists(hostAssembly).ShouldBeTrue(
                "Build the scenario host first: dotnet build tests/Rehost.WebForms.ScenarioHost");

            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = HostDirectory,
            };
            startInfo.ArgumentList.Add(hostAssembly);
            startInfo.ArgumentList.Add("--serve");
            startInfo.ArgumentList.Add("--app");
            startInfo.ArgumentList.Add(applicationPath);
            startInfo.ArgumentList.Add("--temp");
            startInfo.ArgumentList.Add(temp);
            startInfo.ArgumentList.Add("--trace");
            startInfo.ArgumentList.Add(tracePath);
            startInfo.ArgumentList.Add("--response-dir");
            startInfo.ArgumentList.Add(responses);

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

            return new ScenarioRun(root, applicationPath, File.ReadAllLines(tracePath).ToList());
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

        private static string HostDirectory { get; } = FindHostDirectory();

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

            var repositoryRoot = directory?.FullName
                ?? throw new InvalidOperationException("Repository root was not found.");

            return Path.Combine(
                repositoryRoot,
                "tests",
                "Rehost.WebForms.ScenarioHost",
                "bin",
                "Debug",
                "net10.0");
        }
    }
}
