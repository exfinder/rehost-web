using System.Web.Util;
using Rehost.Web.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Util;

public sealed class HiddenFileTests
{
    [Fact]
    public void A_Dot_Prefixed_Name_Is_An_Ordinary_File()
    {
        using var directory = new TempDirectory("rehost-hidden-");
        var path = directory.Path(".profile.browser");
        File.WriteAllText(path, "");

        HiddenFile.IsHidden(new FileInfo(path)).ShouldBeFalse();
    }

    [Fact]
    public void The_Hidden_Attribute_Hides_A_File()
    {
        Assert.SkipWhen(OperatingSystem.IsLinux(), "Linux filesystems carry no hidden attribute.");
        using var directory = new TempDirectory("rehost-hidden-");
        var path = directory.Path("secret.browser");
        File.WriteAllText(path, "");
        File.SetAttributes(path, FileAttributes.Hidden);

        HiddenFile.IsHidden(new FileInfo(path)).ShouldBeTrue();
    }
}
