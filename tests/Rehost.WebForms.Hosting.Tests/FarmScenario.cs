namespace Rehost.WebForms.Hosting.Tests;

public sealed class FarmScenario : IDisposable
{
    internal ScenarioRun Run { get; } = ScenarioRun.Farm(
        "captured",
        "captured-without-event-validation");

    public void Dispose()
    {
        Run.Dispose();
    }
}
