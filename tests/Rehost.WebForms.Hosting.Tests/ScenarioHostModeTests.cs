using System.Diagnostics;
using Rehost.WebForms.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Raw argument lists on purpose: the mode-locked builders cannot spell these invocations, and
// the guard under test is the host's own.
public sealed class ScenarioHostModeTests
{
    [Fact]
    public void Serve_Mode_Refuses_A_Batch_Only_Option()
    {
        var (exitCode, standardError) =
            RunHost("--serve", "--hold-gate", @"Local\rehost-mode-guard-test");

        exitCode.ShouldBe(1);
        standardError.ShouldContain("--hold-gate is not honored in --serve mode");
    }

    [Fact]
    public void Batch_Mode_Refuses_A_Serve_Only_Option()
    {
        var (exitCode, standardError) = RunHost("--http2");

        exitCode.ShouldBe(1);
        standardError.ShouldContain("--http2 is not honored in batch mode");
    }

    [Fact]
    public void Serve_Mode_Refuses_Mixing_Requests_With_Postbacks()
    {
        var (exitCode, standardError) = RunHost(
            "--serve", "--request", "/default", "--postback", "captured");

        exitCode.ShouldBe(1);
        standardError.ShouldContain("--request and --postback cannot be combined in --serve mode");
    }

    private static (int ExitCode, string StandardError) RunHost(params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = ScenarioHostInvocation.HostDirectory,
        };
        startInfo.ArgumentList.Add(ScenarioHostInvocation.HostAssemblyPath);
        foreach (var argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var host = new ScenarioHostProcess(Process.Start(startInfo)!);
        host.WaitForExit();

        return (host.ExitCode, host.StandardError);
    }
}
