using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

// The parity host permanently mutates process-global state (the default AssemblyLoadContext
// resolver, the HttpRuntime singleton, the activated application), so it can never share a
// process with other tests. It is driven as a child process instead.
public sealed class PortableParityGateTests
{
    [Fact]
    public void Cold_synchronous_request_matches_the_framework_golden_trace()
    {
        var (exitCode, standardError) = RunHost("verify");

        exitCode.ShouldBe(0, standardError);
    }

    // BuildManager combined the preserved hash file path with an embedded backslash, which off
    // Windows produced one file named "hash\hash.web" beside the codegen root instead of a file
    // inside it. Nothing observable in the response changes, so only the artifact detects it.
    [Fact]
    public void Preserved_hash_file_nests_under_the_codegen_hash_directory()
    {
        var codegenRoot = Path.Combine(HostDirectory, "fixture", "temp", "root");

        if (Directory.Exists(codegenRoot))
        {
            Directory.Delete(codegenRoot, recursive: true);
        }

        var (exitCode, standardError) = RunHost("run");
        exitCode.ShouldBe(0, standardError);

        File.Exists(Path.Combine(codegenRoot, "hash", "hash.web")).ShouldBeTrue();
        Directory
            .EnumerateFileSystemEntries(codegenRoot, "*", SearchOption.AllDirectories)
            .ShouldNotContain(entry => Path.GetFileName(entry).Contains('\\'));
    }

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string HostDirectory { get; } = Path.Combine(
        RepositoryRoot,
        "prototypes",
        "portable-parity",
        "src",
        "PortableParity.Host",
        "bin",
        "Release",
        "net10.0");

    private static (int ExitCode, string StandardError) RunHost(string command)
    {
        var hostAssembly = Path.Combine(HostDirectory, "PortableParity.Host.dll");

        File.Exists(hostAssembly).ShouldBeTrue(
            "Build the parity prototype first: dotnet build prototypes/portable-parity/PortableParity.slnx -c Release");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepositoryRoot
        };
        startInfo.ArgumentList.Add(hostAssembly);
        startInfo.ArgumentList.Add(command);

        using var process = Process.Start(startInfo)!;
        var standardError = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, standardError);
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
