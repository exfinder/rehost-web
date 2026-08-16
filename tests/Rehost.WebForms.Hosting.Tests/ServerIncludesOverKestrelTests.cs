using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// <!-- #include file="..." --> over the shared page host. A relative name that stays inside the
// application resolves virtually; one that climbs above the root falls back to a path composed
// physically from the including page's directory, with either separator (ledger P71; IIS
// Express reading 2026-08-16). The outside include is written beside the application copy
// before any request so the directory's batch compile never sees it missing.
public sealed class ServerIncludesOverKestrelTests : IClassFixture<PageLiveScenario>
{
    private readonly PageLiveScenario _scenario;

    public ServerIncludesOverKestrelTests(PageLiveScenario scenario)
    {
        _scenario = scenario;
        OutsideInclude.Write(scenario.ApplicationPath);
    }

    [Fact]
    public async Task A_Relative_Include_Inside_The_Application_Resolves_Virtually()
    {
        var response = await _scenario.Client.GetAsync("/ssi/Inside.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("ssi:banner[inside]:end");
    }

    [Fact]
    public async Task An_Include_Climbing_Above_The_Root_Resolves_Physically()
    {
        var response = await _scenario.Client.GetAsync("/ssi/Escape.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("ssi:banner[outside]:end");
    }

    [Fact]
    public async Task A_Backslash_Include_Climbing_Above_The_Root_Resolves_Physically()
    {
        var response = await _scenario.Client.GetAsync("/ssi/EscapeBack.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("ssi:banner[outside]:end");
    }
}

// The escaping fixture pages include ../../shared/Banner.inc relative to /ssi, which lands
// beside the application copy in the host's disposable root.
internal static class OutsideInclude
{
    internal static void Write(string applicationPath)
    {
        var shared = Directory.CreateDirectory(Path.Combine(applicationPath, "..", "shared"));
        File.WriteAllText(
            Path.Combine(shared.FullName, "Banner.inc"), "banner[<%= \"outside\" %>]");
    }
}
