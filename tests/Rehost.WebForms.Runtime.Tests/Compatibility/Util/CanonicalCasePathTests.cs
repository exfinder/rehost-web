using System.Web;
using System.Web.Util;
using Shouldly;
using Xunit;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Util;

// Every case needs a filesystem that treats File.txt and file.txt as distinct; the fixture
// provides one or the test skips (Windows/NTFS cannot express the situation).
public sealed class CanonicalCasePathTests(CaseSensitiveVolume volume)
    : IClassFixture<CaseSensitiveVolume>
{
    private string RequireRoot()
    {
        var root = Path.Combine(volume.RequirePath(), "app-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void A_Wrongly_Cased_Path_Resolves_To_The_Real_Casing()
    {
        var root = RequireRoot();
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        File.WriteAllText(Path.Combine(root, "Sub", "Default.aspx"), "");

        var resolved = CanonicalCasePath.Resolve(
            Path.Combine(root, "sub", "default.aspx"), root);

        resolved.ShouldBe(Path.Combine(root, "Sub", "Default.aspx"));
    }

    [Fact]
    public void An_Exact_Path_Is_Returned_Unchanged_Even_Beside_A_Case_Variant()
    {
        var root = RequireRoot();
        File.WriteAllText(Path.Combine(root, "File.txt"), "");
        File.WriteAllText(Path.Combine(root, "file.txt"), "");

        CanonicalCasePath.Resolve(Path.Combine(root, "file.txt"), root)
            .ShouldBe(Path.Combine(root, "file.txt"));
    }

    [Fact]
    public void An_Ambiguous_Fallback_Fails_Naming_Both_Candidates()
    {
        var root = RequireRoot();
        File.WriteAllText(Path.Combine(root, "File.txt"), "");
        File.WriteAllText(Path.Combine(root, "fILE.txt"), "");

        var exception = Should.Throw<HttpException>(
            () => CanonicalCasePath.Resolve(Path.Combine(root, "file.txt"), root));

        exception.GetHttpCode().ShouldBe(500);
        exception.Message.ShouldContain("File.txt", Case.Sensitive);
        exception.Message.ShouldContain("fILE.txt", Case.Sensitive);
    }

    [Fact]
    public void A_Missing_Path_Is_Returned_Unchanged()
    {
        var root = RequireRoot();

        var missing = Path.Combine(root, "nope", "gone.aspx");
        CanonicalCasePath.Resolve(missing, root).ShouldBe(missing);
    }

    [Fact]
    public void A_Path_Outside_The_Application_Root_Folds_Below_Its_Nearest_Existing_Directory()
    {
        var root = RequireRoot();
        var app = Path.Combine(root, "app");
        Directory.CreateDirectory(Path.Combine(root, "Shared", "Inc"));
        File.WriteAllText(Path.Combine(root, "Shared", "Inc", "Banner.inc"), "");

        var resolved = CanonicalCasePath.Resolve(
            Path.Combine(root, "shared", "inc", "banner.inc"), app);

        resolved.ShouldBe(Path.Combine(root, "Shared", "Inc", "Banner.inc"));
    }

    [Fact]
    public void A_Missing_Path_Outside_The_Application_Root_Is_Returned_Unchanged()
    {
        var root = RequireRoot();
        var missing = Path.Combine(root, "shared", "gone.inc");

        CanonicalCasePath.Resolve(missing, Path.Combine(root, "app")).ShouldBe(missing);
    }

    [Fact]
    public void A_Relative_Path_Is_Returned_Unchanged()
    {
        RequireRoot();
        var relative = Path.Combine("shared", "banner.inc");

        CanonicalCasePath.Resolve(relative, null).ShouldBe(relative);
    }

    [Fact]
    public void A_Trailing_Separator_Survives_Resolution()
    {
        var root = RequireRoot();
        Directory.CreateDirectory(Path.Combine(root, "Themes"));

        var resolved = CanonicalCasePath.Resolve(
            Path.Combine(root, "themes") + Path.DirectorySeparatorChar, root);

        resolved.ShouldBe(Path.Combine(root, "Themes") + Path.DirectorySeparatorChar);
    }
}
