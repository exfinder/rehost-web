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
}
