using System.Web.IisConfig;
using Rehost.WebForms.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// NTFS folded the casing of a row's path for IIS; a case-sensitive volume misses instead, so the
// spelling the row resolves to has to be the one on disk for the request-time read to find it.
public sealed class HttpErrorsCaseSensitivityTests(CaseSensitiveVolume volume)
    : IClassFixture<CaseSensitiveVolume>
{
    [Fact]
    public void A_File_Row_Spelled_In_Another_Casing_Resolves_To_The_Spelling_On_Disk()
    {
        var root = Path.Combine(volume.RequirePath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Err404.htm"), "custom");

        var application = Path.Combine(root, "web.config");
        File.WriteAllText(
            application,
            """
            <?xml version="1.0"?>
            <configuration>
            <system.webServer>
              <httpErrors>
                <remove statusCode="404" />
                <error statusCode="404" path="err404.htm" />
              </httpErrors>
            </system.webServer>
            </configuration>
            """);

        var row = IisServerConfiguration.Load(
                Path.Combine(
                    AppContext.BaseDirectory, "configs", "rehost-webforms.applicationHost.config"),
                application)
            .HttpErrors.Find(404, 0)
            .ShouldNotBeNull();

        Path.GetFileName(row.PhysicalPath!).ShouldBe("Err404.htm");
        row.ContentType.ShouldBe("text/html");
        File.Exists(row.PhysicalPath!).ShouldBeTrue();
    }
}
