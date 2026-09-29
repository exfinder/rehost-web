using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// The path-taking sites reached in PathClassificationOverKestrelTests, spelled in the wrong
// case on a genuinely case-sensitive filesystem: those that map through the runtime's seams
// fold there (ledger P57), and a server include composed physically above the root folds from
// the nearest existing directory (ledger P71). Skips where no such filesystem exists.
public sealed class PathCasingOverKestrelTests(CaseSensitiveLiveScenario scenario)
{
    [Fact]
    public async Task A_Wrongly_Cased_Include_Above_The_Root_Folds_To_The_Real_File()
    {
        var live = scenario.RequireLive();
        OutsideInclude.Write(live.ApplicationPath);

        var response = await live.Client.GetAsync("/ssi/EscapeLower.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldBe("ssi:banner[outside]:end");
    }

    [Fact]
    public async Task A_Wrongly_Cased_Configured_Master_Page_Composes_The_Page()
    {
        var live = scenario.RequireLive();

        var response = await live.Client.GetAsync("/master-cfg/Page.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.Trim().ShouldBe("cfg-master[page]");
    }

    // The provider's default siteMapFile is web.sitemap; Visual Studio writes Web.sitemap.
    [Fact]
    public async Task The_Site_Map_File_Is_Found_Under_Its_Real_Casing()
    {
        var live = scenario.RequireLive();

        var response = await live.Client.GetAsync("/sitemap/Show.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.ShouldStartWith("rooted=/sitemap/A.aspx|key=/sitemap/a.aspx\n", Case.Sensitive);
    }

    [Fact]
    public async Task Wrongly_Cased_Rooted_Paths_Fold_At_Data_Source_Mail_And_File_Sites()
    {
        var live = scenario.RequireLive();

        (await Probe(live, "xml", "/paths/data.xml")).ShouldBe("xml=<r><i n=\"site\" /></r>");
        (await Probe(live, "mail", "/paths/body.txt")).ShouldBe("body=site-body");
        (await Probe(live, "open", "/paths/body.txt")).ShouldBe("open=site-body");
        (await Probe(live, "w", "/paths/x.txt")).ShouldBe("write=site-file");
        (await Probe(live, "t", "/paths/x.txt")).ShouldBe("transmit=site-file");
    }

    private static async Task<string> Probe(LiveScenario live, string kind, string path)
    {
        var response = await live.Client.GetAsync(
            "/paths/Files.aspx?k=" + kind + "&p=" + Uri.EscapeDataString(path));
        response.StatusCode.ShouldBe(200);
        return response.Text;
    }
}
