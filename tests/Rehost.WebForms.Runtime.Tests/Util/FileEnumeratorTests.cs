using System.Web.Util;
using Shouldly;
using Xunit;
using Rehost.WebForms.TestSupport;

namespace Rehost.WebForms.Runtime.Tests.Util;

// The order is NTFS's: names compared code unit by code unit after upper-casing, so digits sort
// before letters, '_' after 'Z', and directories interleave with files. ext4 and APFS return hash
// order, so an unsorted enumerator fails this on both.
public sealed class FileEnumeratorTests : IClassFixture<FileEnumeratorTests.Volume>
{
    public sealed class Volume : IDisposable
    {
        internal CaseSensitiveDirectory Directory { get; } = CaseSensitiveDirectory.Create();

        public void Dispose() => Directory.Dispose();
    }

    private readonly string? _caseSensitiveRoot;

    public FileEnumeratorTests(Volume volume)
    {
        _caseSensitiveRoot = volume.Directory.Path;
    }

    [Fact]
    public void Enumerates_Entries_In_Ntfs_Order()
    {
        var directory = Directory.CreateTempSubdirectory("rehost-enum-order-");
        try
        {
            foreach (var name in new[]
                     {
                         "zeta.cs", "Yankee.cs", "x_ray.cs", "whiskey.cs", "Victor.cs",
                         "uniform.cs", "9nine.cs", "_under.cs", "Alpha.cs", "bravo.cs",
                     })
            {
                File.WriteAllText(Path.Combine(directory.FullName, name), "");
            }

            Directory.CreateDirectory(Path.Combine(directory.FullName, "Mid"));

            Names(directory.FullName).ShouldBe(
            [
                "9nine.cs", "Alpha.cs", "bravo.cs", "Mid", "uniform.cs", "Victor.cs",
                "whiskey.cs", "x_ray.cs", "Yankee.cs", "zeta.cs", "_under.cs",
            ]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Names_Differing_Only_By_Case_Sort_Uppercase_First()
    {
        Assert.SkipWhen(_caseSensitiveRoot == null, "No case-sensitive filesystem is available on this platform.");
        var directory = Path.Combine(_caseSensitiveRoot!, "enum-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (var name in new[] { "b.cs", "a.cs", "B.cs", "A.cs" })
        {
            File.WriteAllText(Path.Combine(directory, name), "");
        }

        Names(directory).ShouldBe(["A.cs", "a.cs", "B.cs", "b.cs"]);
    }

    private static List<string> Names(string directory)
    {
        var names = new List<string>();
        foreach (FileData entry in FileEnumerator.Create(directory))
        {
            names.Add(entry.Name);
        }

        return names;
    }
}
