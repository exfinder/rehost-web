using Rehost.WebForms.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Hosting.Tests;

public sealed class DirectoryAwareFileProviderTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-fileprovider-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void A_Folder_Exists_As_A_Directory()
    {
        Directory.CreateDirectory(Path.Combine(_root.FullName, "sub"));

        var entry = Provider().GetFileInfo("/sub");

        entry.Exists.ShouldBeTrue();
        entry.IsDirectory.ShouldBeTrue();
        entry.PhysicalPath.ShouldBe(Path.Combine(_root.FullName, "sub"));
    }

    [Fact]
    public void A_File_Stays_A_File()
    {
        File.WriteAllText(Path.Combine(_root.FullName, "static.txt"), "x");

        var entry = Provider().GetFileInfo("/static.txt");

        entry.Exists.ShouldBeTrue();
        entry.IsDirectory.ShouldBeFalse();
        entry.Length.ShouldBe(1);
    }

    [Fact]
    public void A_Missing_Path_Does_Not_Exist()
    {
        Provider().GetFileInfo("/nosuch/thing").Exists.ShouldBeFalse();
    }

    [Fact]
    public void A_Path_Climbing_Out_Of_The_Application_Does_Not_Exist()
    {
        Provider().GetFileInfo("/../" + _root.Name).Exists.ShouldBeFalse();
    }

    private DirectoryAwareFileProvider Provider() => new(_root.FullName);
}
