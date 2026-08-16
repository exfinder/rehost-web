using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

// The http.sys canonicalization the adapter finishes after Kestrel (IIS reading, ledger P72).
public sealed class RequestPathCanonicalizerTests
{
    [Theory]
    [InlineData("/sub/../echo.probe", "/echo.probe")]
    [InlineData("/./echo.probe", "/echo.probe")]
    [InlineData("/sub/./echo.probe", "/sub/echo.probe")]
    [InlineData("/echo.probe/.", "/echo.probe/")]
    [InlineData("/echo.probe/..", "/")]
    [InlineData("/sub%2Fecho.probe", "/sub/echo.probe")]
    [InlineData("/sub%2f..%2fecho.probe", "/echo.probe")]
    [InlineData("/echo.probe%2Fextra", "/echo.probe/extra")]
    [InlineData("/sub\\echo.probe", "/sub/echo.probe")]
    [InlineData("/echo.probe\\", "/echo.probe/")]
    [InlineData("//echo.probe", "/echo.probe")]
    [InlineData("/sub//echo.probe", "/sub/echo.probe")]
    [InlineData("/echo.probe//", "/echo.probe/")]
    [InlineData("/sub dir/echo.probe", "/sub dir/echo.probe")]
    [InlineData("/", "/")]
    public void Separators_Encoded_Slashes_And_Dot_Segments_Resolve_As_On_Http_Sys(
        string path, string canonical)
    {
        RequestPathCanonicalizer.Canonicalize(path, out var escapesRoot).ShouldBe(canonical);
        escapesRoot.ShouldBeFalse();
    }

    [Theory]
    [InlineData("/../echo.probe")]
    [InlineData("/..")]
    [InlineData("/sub/../../../x.txt")]
    [InlineData("/sub/../../outside/x.txt")]
    [InlineData("/sub/..\\..\\web.config")]
    [InlineData("/%2e%2e/x.txt")]
    [InlineData("/link/../../outside/x.txt?q=1")]
    public void A_Climb_Above_The_Root_Is_Reported_From_The_Raw_Target(string rawTarget)
    {
        RequestPathCanonicalizer.EscapesRoot(rawTarget).ShouldBeTrue();
    }

    [Theory]
    [InlineData("/sub/../echo.probe")]
    [InlineData("/echo.probe/..")]
    [InlineData("/echo.probe?a=%2F..%2F..%2Fx")]
    [InlineData("/x%25")]
    [InlineData(null)]
    [InlineData("")]
    public void A_Climb_Inside_The_Root_Or_In_The_Query_Is_Not(string? rawTarget)
    {
        RequestPathCanonicalizer.EscapesRoot(rawTarget).ShouldBeFalse();
    }
}
