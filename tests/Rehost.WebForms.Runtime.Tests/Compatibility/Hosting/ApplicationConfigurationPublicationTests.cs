using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

// Framework published the application's configuration into AppDomain data from the AppDomain
// creation path. There is no child AppDomain here, so the portable path publishes it instead.
// UnobtrusiveValidationMode is the cheapest observable: it reads straight off BinaryCompatibility.
public sealed class ApplicationConfigurationPublicationTests
{
    [Fact]
    public void A_Declared_Target_Framework_Reaches_The_Quirks_Switch()
    {
        var trace = Run("modern-target");

        trace.ShouldContain("unobtrusive-validation:WebForms");
    }

    // Framework hands a startup configuration failure to HostingEnvironment.Initialize rather than
    // aborting, so activation completes and every request renders it.
    [Fact]
    public void An_Application_Declaring_No_Target_Framework_Is_Refused_Per_Request()
    {
        var trace = Run("legacy-target");

        trace.ShouldContain("request:/quirks:500");
        trace.ShouldContain(
            entry => entry.StartsWith("error-body:") && entry.Contains("targetFramework"),
            "The rendered error should name the missing targetFramework.");
    }

    private static List<string> Run(string fixture)
    {
        var (exitCode, standardError, trace) = Start(fixture);

        exitCode.ShouldBe(0, standardError);
        return trace;
    }

    private static (int ExitCode, string StandardError, List<string> Trace) Start(string fixture)
    {
        var root = Directory.CreateTempSubdirectory("rehost-quirks-");
        try
        {
            var tracePath = Path.Combine(root.FullName, "trace.txt");
            var temp = Path.Combine(root.FullName, "temp");
            Directory.CreateDirectory(temp);

            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = HostDirectory,
            };
            startInfo.ArgumentList.Add(Path.Combine(HostDirectory, "Rehost.WebForms.ScenarioHost.dll"));
            startInfo.ArgumentList.Add("--app");
            startInfo.ArgumentList.Add(Path.Combine(HostDirectory, "fixtures", fixture));
            startInfo.ArgumentList.Add("--temp");
            startInfo.ArgumentList.Add(temp);
            startInfo.ArgumentList.Add("--trace");
            startInfo.ArgumentList.Add(tracePath);
            startInfo.ArgumentList.Add("--request");
            startInfo.ArgumentList.Add("/quirks");

            using var process = Process.Start(startInfo)!;
            var standardError = process.StandardError.ReadToEndAsync();
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            Task.WaitAll(standardError, standardOutput);
            process.WaitForExit();

            var trace = File.Exists(tracePath)
                ? File.ReadAllLines(tracePath).ToList()
                : new List<string>();

            return (process.ExitCode, standardError.Result, trace);
        }
        finally
        {
            try
            {
                root.Delete(recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string HostDirectory { get; } = FindHostDirectory();

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
