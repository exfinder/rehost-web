namespace Rehost.WebForms.Hosting.Tests;

public sealed class AsyncAppLiveScenario : LiveScenario
{
    public AsyncAppLiveScenario()
        : base(Fixtures.AsyncApp)
    {
    }
}
