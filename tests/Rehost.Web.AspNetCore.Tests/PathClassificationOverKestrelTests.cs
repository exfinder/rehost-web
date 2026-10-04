using Shouldly;
using Xunit;

namespace Rehost.Web.AspNetCore.Tests;

// Framework never reads a '/'-rooted string as a physical path: at every path-taking site it is
// a virtual path, a relative one resolves against the page, one climbing above the root is
// refused, and only X:\ and \\server shapes are physical. The port's untouched classifier takes
// the same branches on every OS; these pin the IIS Express reading of 2026-08-16 (ledger P71)
// for the sites the known-physical follow-up named.
public sealed class PathClassificationOverKestrelTests(PageLiveScenario scenario)
    : IClassFixture<PageLiveScenario>
{
    private const string LeadingDotDot =
        "EX HttpException: Cannot use a leading .. to exit above the top directory.";

    private async Task<string> Probe(string kind, string path)
    {
        var response = await scenario.Client.GetAsync(
            "/paths/Files.aspx?k=" + kind + "&p=" + Uri.EscapeDataString(path));
        response.StatusCode.ShouldBe(200);
        return response.Text;
    }

    [Fact]
    public async Task Rooted_And_Relative_Paths_Are_Virtual_At_Every_Site()
    {
        (await Probe("xml", "/paths/Data.xml")).ShouldBe("xml=<r><i n=\"site\" /></r>");
        (await Probe("xml", "Data.xml")).ShouldBe("xml=<r><i n=\"site\" /></r>");
        (await Probe("mail", "/paths/Body.txt")).ShouldBe("body=site-body");
        (await Probe("mail", "Body.txt")).ShouldBe("body=site-body");
        (await Probe("open", "/paths/Body.txt")).ShouldBe("open=site-body");
        (await Probe("open", "Body.txt")).ShouldBe(
            "EX ArgumentException: The relative virtual path 'Body.txt' is not allowed here.");
        (await Probe("secure", "/paths/X.txt")).ShouldBe(
            "secure=" + Path.Combine(scenario.ApplicationPath, "paths", "X.txt"));
        (await Probe("w", "/paths/X.txt")).ShouldBe("write=site-file");
        (await Probe("w", "X.txt")).ShouldBe("write=site-file");
        (await Probe("t", "/paths/X.txt")).ShouldBe("transmit=site-file");
        (await Probe("t", "X.txt")).ShouldBe("transmit=site-file");
        (await Probe("map", "/paths/X.txt")).ShouldBe(
            "map=" + Path.Combine(scenario.ApplicationPath, "paths", "X.txt"));
        (await Probe("rmap", "X.txt")).ShouldBe(
            "rmap=" + Path.Combine(scenario.ApplicationPath, "paths", "X.txt"));
    }

    [Fact]
    public async Task Response_File_Apis_Take_A_Mapped_Physical_Path_As_Is()
    {
        var physical = Path.Combine(scenario.ApplicationPath, "paths", "X.txt");

        (await Probe("w", physical)).ShouldBe("write=site-file");
        (await Probe("t", physical)).ShouldBe("transmit=site-file");
        (await Probe("w", physical.ToUpperInvariant())).ShouldBe("write=site-file");
        (await Probe("t", physical.ToUpperInvariant())).ShouldBe("transmit=site-file");
    }

    [Fact]
    public async Task A_Relative_Path_Climbing_Above_The_Root_Is_Refused_At_Every_Site()
    {
        foreach (var kind in new[] { "xml", "mail", "secure", "map", "rmap" })
        {
            (await Probe(kind, "../../outside/x.txt")).ShouldBe(LeadingDotDot, kind);
        }

        (await Probe("w", "../../outside/x.txt")).ShouldBe("write=" + LeadingDotDot);
        (await Probe("t", "../../outside/x.txt")).ShouldBe("transmit=" + LeadingDotDot);
        (await Probe("open", "../../outside/x.txt")).ShouldBe(
            "EX ArgumentException: The relative virtual path '../../outside/x.txt' is not allowed here.");
    }

    // A '//server/share' string is UNC-physical for file APIs and collapses to a virtual path
    // for MapPath; the share does not exist here or on Framework, so file APIs fail with an
    // I/O exception rather than serving anything from under the application.
    [Fact]
    public async Task A_Double_Slash_Prefix_Is_Physical_For_File_Apis_And_Virtual_For_MapPath()
    {
        var write = await Probe("w", "//srv/share/x.txt");
        var transmit = await Probe("t", "//srv/share/x.txt");

        write.ShouldStartWith("write=EX ", Case.Sensitive);
        write.ShouldNotContain("site-file");
        transmit.ShouldStartWith("transmit=EX ", Case.Sensitive);
        transmit.ShouldNotContain("site-file");
        (await Probe("map", "//srv/share/x.txt")).ShouldBe(
            "map=" + Path.Combine(scenario.ApplicationPath, "srv", "share", "x.txt"));
    }

    [Fact]
    public async Task Site_Map_Node_Urls_Classify_As_On_Framework()
    {
        var response = await scenario.Client.GetAsync("/sitemap/Show.aspx");

        response.StatusCode.ShouldBe(200, response.Text);
        response.Text.ShouldBe(
            "rooted=/sitemap/A.aspx|key=/sitemap/a.aspx\n"
            + "tilde=/sitemap/B.aspx|key=/sitemap/b.aspx\n"
            + "relative=/sitemap/C.aspx|key=/sitemap/c.aspx\n"
            + "physical=C:\\outside\\D.aspx|key=c:\\outside\\d.aspx\n"
            + "unc=//srv/share/E.aspx|key=//srv/share/e.aspx\n"
            + "find(/sitemap/A.aspx)=rooted\n"
            + "find(~/sitemap/B.aspx)=tilde\n"
            + "find(/sitemap/C.aspx)=relative\n"
            + "find(C:\\outside\\D.aspx)=physical\n"
            + "find(//srv/share/E.aspx)=unc\n");
    }

    [Fact]
    public async Task A_Configured_Master_Page_Composes_The_Page()
    {
        var response = await scenario.Client.GetAsync("/master-cfg/Page.aspx");

        response.StatusCode.ShouldBe(200);
        response.Text.Trim().ShouldBe("cfg-master[page]");
    }
}
