using System.Diagnostics;
using Shouldly;

namespace Rehost.WebForms.Hosting.Tests;

internal sealed class ScenarioRun : IDisposable
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

    internal string ResponseText(int index) =>
        File.ReadAllText(Path.Combine(_root.FullName, "responses", index + ".body"));

    internal byte[] Response(string label, int occurrence = 0) => Response(IndexOf(label, occurrence));

    internal string ResponseText(string label, int occurrence = 0) =>
        ResponseText(IndexOf(label, occurrence));

    // Responses are numbered in the order they were recorded, and so are the trace's request
    // lines, so a label locates one without the caller counting positions. A label repeats when a
    // probe issues more than one request, hence the occurrence.
    private int IndexOf(string label, int occurrence)
    {
        var seen = 0;
        var index = 0;

        foreach (var entry in Trace)
        {
            if (!entry.StartsWith("request:", StringComparison.Ordinal))
            {
                continue;
            }

            // An aborted probe records a request line but no response, so it must not advance the
            // ordinal. Its status is a word rather than a code.
            var status = entry.LastIndexOf(':');
            if (!int.TryParse(entry[(status + 1)..], out _))
            {
                continue;
            }

            if (entry[8..status] == label && seen++ == occurrence)
            {
                return index;
            }

            index++;
        }

        throw new InvalidOperationException(
            $"The trace has no request '{label}' at occurrence {occurrence}: "
                + string.Join(", ", Trace));
    }

    internal static ScenarioRun Serve(params string[] requests)
    {
        return Run("page", "--request", requests);
    }

    internal static ScenarioRun ServeBody(string fixture, params string[] probes)
    {
        return Run(fixture, "--body-probe", probes);
    }

    // Responses are numbered in order across probes: one render, then one per postback round.
    internal static ScenarioRun Postback(params string[] probes)
    {
        return Run("postback", "--postback", probes);
    }

    internal static ScenarioRun Farm(params string[] probes)
    {
        return Run("farm", "--postback", probes);
    }

    private static ScenarioRun Run(string fixture, string argument, string[] values)
    {
        var root = Directory.CreateTempSubdirectory("rehost-page-kestrel-");
        var applicationPath = Path.Combine(root.FullName, "app");
        var responses = Path.Combine(root.FullName, "responses");
        var temp = Path.Combine(root.FullName, "temp");
        var tracePath = Path.Combine(root.FullName, "trace.txt");

        CopyDirectory(Path.Combine(HostDirectory, "fixtures", fixture), applicationPath);
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

        foreach (var value in values)
        {
            startInfo.ArgumentList.Add(argument);
            startInfo.ArgumentList.Add(value);
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

        // .../bin/<configuration>/net10.0/ — the host is built in whatever configuration this
        // test assembly was, so the scenario can never run a stale configuration's binaries.
        var configuration = Path.GetFileName(
            Path.GetDirectoryName(
                Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)))!;

        return Path.Combine(
            repositoryRoot,
            "tests",
            "Rehost.WebForms.ScenarioHost",
            "bin",
            configuration,
            "net10.0");
    }
}
