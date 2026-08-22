using System.Diagnostics;
using Rehost.WebForms.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Drives the staging targets against a scratch TargetDir; the live output tree, which running
// hosts serve from, is never touched.
public sealed class FixtureStagingTests : IDisposable
{
    private readonly TempDirectory _target = new("rehost-staging-probe-");

    [Fact]
    public void A_Staged_File_Mutated_In_Place_Is_Restored()
    {
        RunStaging();
        var config = Staged("body", "web.config");
        File.WriteAllText(config, "<configuration />");
        File.SetLastWriteTimeUtc(config, DateTime.UtcNow.AddHours(1));

        RunStaging();

        File.ReadAllText(config).ShouldContain("path=\"cookies\"");
    }

    [Fact]
    public void A_Nested_Directory_Without_Source_Is_Pruned()
    {
        RunStaging();
        Directory.CreateDirectory(Staged("page", "ghost", "inner"));

        RunStaging();

        Directory.Exists(Staged("page", "ghost")).ShouldBeFalse();
    }

    private string Staged(params string[] parts) =>
        Path.Combine([_target.Path("fixtures"), .. parts]);

    // PATH's dotnet can lack the SDK global.json pins; the muxer that launched this test run is
    // the one that has it.
    private static string DotnetPath() =>
        Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet"
            ? Environment.ProcessPath!
            : Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    private void RunStaging()
    {
        var project = Path.Combine(
            RepositoryLocator.FindRoot(AppContext.BaseDirectory),
            "tests", "Rehost.WebForms.ScenarioHost", "Rehost.WebForms.ScenarioHost.csproj");
        var startInfo = new ProcessStartInfo(DotnetPath())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("-t:PruneStaleScenarioFixtures");
        startInfo.ArgumentList.Add(
            "-p:TargetDir=" + _target.Path("") + Path.DirectorySeparatorChar);
        startInfo.ArgumentList.Add("-nr:false");

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, output);
    }

    public void Dispose() => _target.Dispose();
}
