namespace Rehost.WebForms.Hosting.Tests;

public sealed class PageScenario : IDisposable
{
    internal const string PageRequest = "/Default.aspx?value=a%26c%20%22q%22%20%C3%A9";

    internal ScenarioRun Run { get; } = ScenarioRun.Serve(PageRequest, "/Default.aspx.cs");

    public void Dispose()
    {
        Run.Dispose();
    }
}
