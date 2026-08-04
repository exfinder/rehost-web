using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

// Running a session permanently mutates process-global state (the default AssemblyLoadContext
// resolver, the HttpRuntime singleton, the activated application), so it can never share a
// process with other tests. The host is driven as a child process, and itself spawns one
// process per session.
public sealed class PortableParityGateTests
{
    [Fact]
    public void Declared_Sessions_Match_The_Framework_Golden_Trace()
    {
        var (exitCode, standardError) = RunHost("verify");

        exitCode.ShouldBe(0, standardError);
    }

    // BuildManager combined the preserved hash file path with an embedded backslash, which off
    // Windows produced one file named "hash\hash.web" beside the codegen root instead of a file
    // inside it. Nothing observable in the response changes, so only the artifact detects it.
    // The generation segment is observable the same way: the two fixtures share one temp root and
    // must not share a segment.
    [Fact]
    public void Generated_Output_Nests_Under_One_Segment_Per_Application()
    {
        var codegenRoot = Path.Combine(HostDirectory, "fixture", "temp", "root");

        if (Directory.Exists(codegenRoot))
        {
            Directory.Delete(codegenRoot, recursive: true);
        }

        var (exitCode, standardError) = RunHost("run");
        exitCode.ShouldBe(0, standardError);

        Directory.EnumerateFiles(codegenRoot).ShouldBeEmpty();

        var segments = Directory.GetDirectories(codegenRoot);
        segments.Length.ShouldBe(2, "the app and app-errors fixtures each own a segment");

        foreach (var segment in segments)
        {
            Path.GetFileName(segment).ShouldMatch("^[0-9a-f]{8}$");
            File.Exists(Path.Combine(segment, "hash", "hash.web")).ShouldBeTrue();
        }

        Directory
            .EnumerateFileSystemEntries(codegenRoot, "*", SearchOption.AllDirectories)
            .ShouldNotContain(entry => Path.GetFileName(entry).Contains('\\'));
    }

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    // .../bin/<configuration>/net10.0/ — the host is built in whatever configuration this test
    // assembly was, so the gate can never run a stale configuration's binaries.
    private static string Configuration { get; } = Path.GetFileName(
        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)))!;

    private static string HostDirectory { get; } = Path.Combine(
        RepositoryRoot,
        "tests",
        "parity",
        "src",
        "Rehost.WebForms.Parity.PortableHost",
        "bin",
        Configuration,
        "net10.0");

    private static (int ExitCode, string StandardError) RunHost(string command)
    {
        var hostAssembly = Path.Combine(HostDirectory, "Rehost.WebForms.Parity.PortableHost.dll");

        File.Exists(hostAssembly).ShouldBeTrue(
            "Build the parity host first: dotnet build Rehost.WebForms.slnx");

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
