namespace Rehost.WebForms.Hosting.Tests;

internal static class Fixtures
{
    internal static readonly ScenarioFixture Page = new("page");
    internal static readonly ScenarioFixture Postback = new("postback");
    internal static readonly ScenarioFixture Farm = new("farm");
    internal static readonly ScenarioFixture Body = new("body");
    internal static readonly ScenarioFixture BodyPreload = new("body-preload");
    internal static readonly ScenarioFixture BodyCustomErrors = new("body-customerrors");
    internal static readonly ScenarioFixture LegacyTarget = new("legacy-target");
    internal static readonly ScenarioFixture ModernTarget = new("modern-target");
    internal static readonly ScenarioFixture Codegen = new("codegen");
    internal static readonly ScenarioFixture Timeout = new("timeout");
    internal static readonly ScenarioFixture WebServer = new("webserver");
    internal static readonly ScenarioFixture AsyncApp = new("asyncapp");
    internal static readonly ScenarioFixture FriendlyUrls = new("friendlyurls");
    internal static readonly ScenarioFixture DefDocDisabled = new("defdoc-disabled");
}
