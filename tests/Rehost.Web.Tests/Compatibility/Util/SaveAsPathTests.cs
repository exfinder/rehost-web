using System.Web;
using System.Web.Util;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Util;

// The guard fires exactly where the two operating systems disagree about what a rooted path is:
// Windows accepts drive letters, a leading backslash, and UNC shares, and Unix accepts none of
// them. On Windows the rule is inert by construction, so every case below that is rooted there
// passes straight through.
public sealed class SaveAsPathTests
{
    [Theory]
    [InlineData(@"C:\uploads\photo.jpg")]
    [InlineData("C:/uploads/photo.jpg")]
    [InlineData("C:photo.jpg")]
    [InlineData(@"\uploads\photo.jpg")]
    [InlineData(@"\\server\share\photo.jpg")]
    public void A_Path_Rooted_Only_On_Windows_Is_Refused_Where_It_Cannot_Be_Rooted(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Should.NotThrow(() => SaveAsPath.RequireUsableRoot(path));
            return;
        }

        var error = Should.Throw<HttpException>(() => SaveAsPath.RequireUsableRoot(path));

        error.Message.ShouldBe(
            "The SaveAs path '" + path + "' is rooted only on Windows, and this process is not "
            + "running on Windows. Supply a path rooted on this platform, or build one with "
            + "Server.MapPath.");
    }

    [Theory]
    [InlineData("/var/uploads/photo.jpg")]
    [InlineData("uploads/photo.jpg")]
    [InlineData("photo.jpg")]
    [InlineData("")]
    [InlineData(null)]
    public void A_Path_This_Platform_Can_Judge_For_Itself_Passes_Through(string? path)
    {
        // A relative path is not this guard's business: SaveAs already refuses one whenever
        // requireRootedSaveAsPath is on, and deliberately allows it when it is off.
        Should.NotThrow(() => SaveAsPath.RequireUsableRoot(path));
    }
}
