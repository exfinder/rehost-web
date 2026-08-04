namespace Rehost.WebForms.Hosting.Tests;

// Both aborts kill their socket mid-read, so they never share a connection with anything and can
// share a process with each other.
public sealed class AbortScenario : IDisposable
{
    internal ScenarioRun Run { get; } = ScenarioRun.ServeBody(Fixtures.Body, "abort", "abort-apm");

    public void Dispose()
    {
        Run.Dispose();
    }
}
