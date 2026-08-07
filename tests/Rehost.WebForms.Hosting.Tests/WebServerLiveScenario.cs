namespace Rehost.WebForms.Hosting.Tests;

public sealed class WebServerLiveScenario : LiveScenario
{
    public WebServerLiveScenario()
        : base(Fixtures.WebServer)
    {
    }
}
