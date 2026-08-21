using System.Reflection;
using System.Web.Services.Discovery;

using Shouldly;
using Xunit;

namespace Rehost.WebForms.WebServices.Tests.Discovery;

// GetRelativePath computed with literal backslashes; .discomap results written off
// Windows carried absolute or rooted paths instead of relative ones.
public sealed class DiscoveryClientProtocolGetRelativePathTests
{
    private static string GetRelativePath(string fullPath, string relativeTo) =>
        (string)typeof(DiscoveryClientProtocol)
            .GetMethod("GetRelativePath", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [fullPath, relativeTo])!;

    private static readonly string Root =
        Path.Combine(Path.GetTempPath(), "disco-root");

    [Fact]
    public void Sibling_File_Relativizes_To_Its_Bare_Name()
    {
        var map = Path.Combine(Root, "results.discomap");
        var file = Path.Combine(Root, "svc.disco");

        GetRelativePath(file, map).ShouldBe("svc.disco");
    }

    [Fact]
    public void Nested_File_Keeps_Its_Subdirectory()
    {
        var map = Path.Combine(Root, "results.discomap");
        var file = Path.Combine(Root, "sub", "svc.disco");

        GetRelativePath(file, map).ShouldBe(Path.Combine("sub", "svc.disco"));
    }

    [Fact]
    public void Cousin_File_Walks_Up_Through_The_Common_Prefix()
    {
        var map = Path.Combine(Root, "a", "results.discomap");
        var file = Path.Combine(Root, "b", "svc.disco");

        GetRelativePath(file, map).ShouldBe(Path.Combine("..", "b", "svc.disco"));
    }
}
