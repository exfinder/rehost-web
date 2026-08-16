using System.Web.Util;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Util;

public sealed class FileUtilTests
{
    [Fact]
    public void PhysicalPathStatus_Reports_Directories_Files_And_Missing_Paths()
    {
        var directory = Directory.CreateTempSubdirectory("rehost-file-status-");
        try
        {
            var filePath = Path.Combine(directory.FullName, "file.txt");
            File.WriteAllText(filePath, "content");

            FileUtil.PhysicalPathStatus(
                directory.FullName,
                directoryExistsOnError: false,
                fileExistsOnError: false,
                out var directoryExists,
                out var isDirectory);

            FileUtil.PhysicalPathStatus(
                filePath,
                directoryExistsOnError: false,
                fileExistsOnError: false,
                out var fileExists,
                out var isFileDirectory);

            FileUtil.PhysicalPathStatus(
                Path.Combine(directory.FullName, "missing"),
                directoryExistsOnError: true,
                fileExistsOnError: false,
                out var missingExists,
                out _);

            directoryExists.ShouldBeTrue();
            isDirectory.ShouldBeTrue();
            fileExists.ShouldBeTrue();
            isFileDirectory.ShouldBeFalse();
            missingExists.ShouldBeFalse();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static readonly char Sep = Path.DirectorySeparatorChar;

    [Fact]
    public void RemoveTrailingDirectoryBackSlash_Strips_The_Platform_Separator_But_Not_The_Root()
    {
        var root = OperatingSystem.IsWindows() ? @"c:\" : "/";
        var nested = Path.Combine(root, "a", "b") + Sep;

        FileUtil.RemoveTrailingDirectoryBackSlash(nested)
            .ShouldBe(Path.Combine(root, "a", "b"));
        FileUtil.RemoveTrailingDirectoryBackSlash(root).ShouldBe(root);
    }

    [Fact]
    public void FixUpPhysicalDirectory_Ends_With_The_Platform_Separator()
    {
        var directory = Directory.CreateTempSubdirectory("rehost-fixup-");
        try
        {
            var fixedUp = FileUtil.FixUpPhysicalDirectory(directory.FullName);

            fixedUp.ShouldBe(directory.FullName + Sep);
            fixedUp.ShouldNotContain(OperatingSystem.IsWindows() ? "/" : "\\");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // The truncated name reaches codegen output shared across processes, so the suffix must be
    // the same constant everywhere; a randomized per-process hash cannot produce it (P38).
    [Fact]
    public void TruncatePathIfNeeded_Appends_A_Stable_Suffix()
    {
        var path = new string('x', 300);

        var truncated = FileUtil.TruncatePathIfNeeded(path, reservedLength: 0);

        truncated.ShouldBe(
            path.Substring(0, 259 - 13)
            + StringUtil.GetNonRandomizedHashCode(path));
        FileUtil.TruncatePathIfNeeded(path, reservedLength: 0).ShouldBe(truncated);
    }

    // The two-arg overload fed FileChangesMonitor through a raw FindFirstFile; the managed walk
    // must report the file name relative to the root and error on a vanished parent.
    [Fact]
    public void FindFile_Reports_The_Name_Relative_To_The_Root_Directory()
    {
        var root = Directory.CreateTempSubdirectory("rehost-findfile-");
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "sub", "deeper"));
            var filePath = Path.Combine(nested.FullName, "page.aspx");
            File.WriteAllText(filePath, "content");

            var hr = FindFileData.FindFile(filePath, root.FullName, out var data);

            hr.ShouldBe(HResults.S_OK);
            data.FileNameLong.ShouldBe(Path.Combine("sub", "deeper", "page.aspx"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void FindFile_Reports_A_Missing_File_Rather_Than_Throwing()
    {
        var root = Directory.CreateTempSubdirectory("rehost-findfile-miss-");
        try
        {
            var hr = FindFileData.FindFile(
                Path.Combine(root.FullName, "sub", "gone.aspx"), root.FullName, out var data);

            hr.ShouldNotBe(HResults.S_OK);
            data.ShouldBeNull();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    // Past MAX_PATH the check cannot canonicalize the whole string, so it walks back one
    // separator at a time until a prefix canonicalizes; the walk has to be on the platform's
    // separator, and '/' cannot itself be the refusal.
    [Fact]
    public void A_Canonical_Path_Past_Max_Path_Is_Not_Suspicious()
    {
        var root = Directory.CreateTempSubdirectory("rehost-long-path-");
        try
        {
            var path = root.FullName;
            while (path.Length <= 300)
            {
                path = Path.Combine(path, new string('d', 40));
            }

            FileUtil.IsSuspiciousPhysicalPath(path).ShouldBeFalse();
            FileUtil.IsSuspiciousPhysicalPath(
                Path.Combine(root.FullName, "..", Path.GetFileName(root.FullName), path[(root.FullName.Length + 1)..]))
                .ShouldBeTrue();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
