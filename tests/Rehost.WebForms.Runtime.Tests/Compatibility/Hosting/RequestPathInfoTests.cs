using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

// IIS's split rule over a claim predicate, row by row from the IIS reading (ledger P72): the
// first segment a mapping claims ends the file path; disk existence plays no part.
public sealed class RequestPathInfoTests
{
    private static bool ProbeMapped(string prefix) =>
        prefix.EndsWith(".probe", StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData("/echo.probe/extra/info", "/echo.probe", "/extra/info")]
    [InlineData("/echo.probe/extra/info.txt", "/echo.probe", "/extra/info.txt")]
    [InlineData("/sub/echo.probe/a.b", "/sub/echo.probe", "/a.b")]
    [InlineData("/echo.probe/", "/echo.probe", "/")]
    [InlineData("/echo.probe/bin/x", "/echo.probe", "/bin/x")]
    [InlineData("/nothere.probe/extra", "/nothere.probe", "/extra")]
    public void A_Claimed_Segment_Ends_The_File_Path(string path, string filePath, string pathInfo)
    {
        RequestPathInfo.Split(path, ProbeMapped).ShouldBe((filePath, pathInfo));
    }

    [Theory]
    [InlineData("/echo.probe")]
    [InlineData("/x.txt/extra")]
    [InlineData("/api.v2/x.txt")]
    [InlineData("/sub/")]
    [InlineData("/")]
    public void An_Unclaimed_Path_Is_All_File_Path(string path)
    {
        RequestPathInfo.Split(path, ProbeMapped).ShouldBe((path, ""));
    }

    // Only segments with an extension are offered; a dotted directory before the handler is
    // asked and declines, and a match inside it still splits at the handler.
    [Fact]
    public void A_Dotted_Directory_Is_Asked_But_A_Later_Claim_Still_Splits()
    {
        var asked = new List<string>();
        var result = RequestPathInfo.Split("/api.v2/echo.probe/extra", prefix =>
        {
            asked.Add(prefix);
            return ProbeMapped(prefix);
        });

        result.ShouldBe(("/api.v2/echo.probe", "/extra"));
        asked.ShouldBe(new[] { "/api.v2", "/api.v2/echo.probe" });
    }

    [Fact]
    public void Segments_Without_An_Extension_Are_Never_Offered()
    {
        var asked = new List<string>();
        RequestPathInfo.Split("/sub/dir/x.txt", prefix => { asked.Add(prefix); return false; });

        asked.ShouldBe(new[] { "/sub/dir/x.txt" });
    }
}
