using System.Diagnostics;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Hosting.Tests;

// One passively-serving host process for a fixture; tests act through the client and assert on
// the response, reaching for the journal only for server-side facts.
public class LiveScenario : IDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);

    private readonly DirectoryInfo _root;
    private readonly ScenarioHostProcess _process;
    private readonly string _tracePath;

    internal LiveScenario(
        ScenarioFixture fixture,
        string? rootPath = null,
        long? kestrelMaxBody = null)
    {
        _root = rootPath == null
            ? Directory.CreateTempSubdirectory("rehost-live-kestrel-")
            : Directory.CreateDirectory(
                Path.Combine(rootPath, "live-" + Guid.NewGuid().ToString("N")));
        ApplicationPath = Path.Combine(_root.FullName, "app");
        _tracePath = Path.Combine(_root.FullName, "trace.txt");
        var temp = Path.Combine(_root.FullName, "temp");
        var responses = Path.Combine(_root.FullName, "responses");

        TestFiles.CopyDirectory(
            ScenarioHostInvocation.FixturePath(fixture.Name),
            ApplicationPath);
        Directory.CreateDirectory(temp);
        Directory.CreateDirectory(responses);

        var invocation = new ScenarioHostInvocation()
            .Serve()
            .Application(ApplicationPath)
            .CompilationTemp(temp)
            .Trace(_tracePath)
            .ResponseDirectory(responses);
        if (kestrelMaxBody != null)
        {
            invocation.KestrelMaxBody(kestrelMaxBody.Value);
        }

        _process = invocation.Start();

        Address = new Uri(WaitForAddress());
        Client = new ScenarioClient(Address);
    }

    internal string ApplicationPath { get; }

    internal int HostProcessId => _process.Id;

    internal Uri Address { get; }

    internal ScenarioClient Client { get; }

    internal WitnessReader Witness => new(Client);

    internal ScenarioJournalReader Journal =>
        ScenarioJournalReader.Parse(TraceFile.ReadLines(_tracePath));

    private string WaitForAddress()
    {
        var deadline = Stopwatch.StartNew();

        while (deadline.Elapsed < StartupTimeout)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    "The scenario host exited before publishing an address. "
                    + _process.StandardError);
            }

            var address = ScenarioJournalReader.Parse(TraceFile.ReadLines(_tracePath)).Address;
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
            + _process.StandardError);
    }

    public void Dispose()
    {
        Client?.Dispose();
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
