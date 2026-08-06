using System.Diagnostics;

namespace Rehost.WebForms.Hosting.Tests;

// One passively-serving host process for a fixture; tests act through the client and assert on
// the response, reaching for the journal only for server-side facts.
public class LiveScenario : IDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);

    private readonly DirectoryInfo _root;
    private readonly Process _process;
    private readonly Task<string> _standardError;
    private readonly Task<string> _standardOutput;
    private readonly string _tracePath;

    internal LiveScenario(ScenarioFixture fixture, params string[] extraHostArgs)
        : this(fixture, null, extraHostArgs)
    {
    }

    internal LiveScenario(ScenarioFixture fixture, string? rootPath, string[] extraHostArgs)
    {
        _root = rootPath == null
            ? Directory.CreateTempSubdirectory("rehost-live-kestrel-")
            : Directory.CreateDirectory(
                Path.Combine(rootPath, "live-" + Guid.NewGuid().ToString("N")));
        ApplicationPath = Path.Combine(_root.FullName, "app");
        _tracePath = Path.Combine(_root.FullName, "trace.txt");
        var temp = Path.Combine(_root.FullName, "temp");
        var responses = Path.Combine(_root.FullName, "responses");

        ScenarioRun.CopyDirectory(
            Path.Combine(ScenarioRun.HostDirectory, "fixtures", fixture.Name),
            ApplicationPath);
        Directory.CreateDirectory(temp);
        Directory.CreateDirectory(responses);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = ScenarioRun.HostDirectory,
        };
        startInfo.ArgumentList.Add(ScenarioRun.HostAssemblyPath);
        startInfo.ArgumentList.Add("--serve");
        startInfo.ArgumentList.Add("--app");
        startInfo.ArgumentList.Add(ApplicationPath);
        startInfo.ArgumentList.Add("--temp");
        startInfo.ArgumentList.Add(temp);
        startInfo.ArgumentList.Add("--trace");
        startInfo.ArgumentList.Add(_tracePath);
        startInfo.ArgumentList.Add("--response-dir");
        startInfo.ArgumentList.Add(responses);
        foreach (var argument in extraHostArgs)
        {
            startInfo.ArgumentList.Add(argument);
        }

        _process = Process.Start(startInfo)!;
        _standardError = _process.StandardError.ReadToEndAsync();
        _standardOutput = _process.StandardOutput.ReadToEndAsync();

        Address = new Uri(WaitForAddress());
        Client = new ScenarioClient(Address);
    }

    internal string ApplicationPath { get; }

    internal Uri Address { get; }

    internal ScenarioClient Client { get; }

    internal WitnessReader Witness => new(Client);

    internal ScenarioJournalReader Journal => ScenarioJournalReader.Parse(ReadTrace());

    private string WaitForAddress()
    {
        var deadline = Stopwatch.StartNew();

        while (deadline.Elapsed < StartupTimeout)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    "The scenario host exited before publishing an address. "
                    + _standardError.Result);
            }

            var address = ScenarioJournalReader.Parse(ReadTrace()).Address;
            if (address != null)
            {
                return address;
            }

            Thread.Sleep(20);
        }

        Dispose();
        throw new TimeoutException(
            "The scenario host did not publish an address within "
            + StartupTimeout.TotalSeconds
            + "s. "
            + _standardError.Result);
    }

    private IEnumerable<string> ReadTrace()
    {
        if (!File.Exists(_tracePath))
        {
            return [];
        }

        // The child keeps the file open for appending.
        using var stream = new FileStream(
            _tracePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    public void Dispose()
    {
        Client?.Dispose();

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.WaitForExit();
        _process.Dispose();

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
}
