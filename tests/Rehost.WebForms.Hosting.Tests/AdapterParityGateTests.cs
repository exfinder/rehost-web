using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// Activating an application permanently mutates process-global state, so the rig is driven as a
// child process which itself spawns one process per session.
public sealed class AdapterParityGateTests
{
    [Fact]
    public void Declared_sessions_match_the_framework_golden_trace_over_kestrel()
    {
        var (exitCode, standardError) = RunHost("verify");

        exitCode.ShouldBe(0, standardError);
    }

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string HostDirectory { get; } = Path.Combine(
        RepositoryRoot,
        "prototypes",
        "adapter-parity",
        "src",
        "AdapterParity.Host",
        "bin",
        "Release",
        "net10.0");

    private static (int ExitCode, string StandardError) RunHost(string command)
    {
        var hostAssembly = Path.Combine(HostDirectory, "AdapterParity.Host.dll");

        File.Exists(hostAssembly).ShouldBeTrue(
            "Build the adapter prototype first: dotnet build prototypes/adapter-parity/AdapterParity.slnx -c Release");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepositoryRoot
        };
        startInfo.ArgumentList.Add(hostAssembly);
        startInfo.ArgumentList.Add(command);

        using var process = Process.Start(startInfo)!;
        // Both streams must drain concurrently. A trace larger than the pipe buffer, which is
        // 4 KB on Windows against 64 KB elsewhere, blocks the host mid-write while a sequential
        // reader waits on the stream it is not draining.
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        Task.WaitAll(standardOutput, standardError);
        process.WaitForExit();

        return (process.ExitCode, standardError.Result);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null
            && !File.Exists(Path.Combine(directory.FullName, "Rehost.WebForms.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
