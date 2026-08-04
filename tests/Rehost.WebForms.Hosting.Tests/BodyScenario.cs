namespace Rehost.WebForms.Hosting.Tests;

// Probes that neither change a host-level limit nor assert over the whole trace; probes that do
// keep their own process, and say why.
public sealed class BodyScenario : IDisposable
{
    internal ScenarioRun Run { get; } = ScenarioRun.ServeBody(
        Fixtures.Body,
        "fixed-input",
        "fixed-binary",
        "fixed-buffered",
        "fixed-bufferless",
        "chunked-bufferless",
        "chunked-apm",
        "expect-continue",
        "spill",
        "too-large",
        "chunked-too-large");

    public void Dispose()
    {
        Run.Dispose();
    }
}
