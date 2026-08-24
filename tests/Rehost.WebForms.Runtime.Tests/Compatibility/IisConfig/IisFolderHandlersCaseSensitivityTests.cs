using System.Configuration;
using System.Web;
using System.Web.IisConfig;
using Rehost.WebForms.TestSupport;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;


// The collision only a case-sensitive filesystem can hold: two directories that differ by case
// are one configuration path, so neither folder's handlers can be the answer.
public sealed class IisFolderHandlersCaseSensitivityTests(CaseSensitiveVolume volume)
    : IClassFixture<CaseSensitiveVolume>
{
    [Fact]
    public void Two_Folder_Configs_Differing_Only_By_Case_Refuse_Activation()
    {
        var temp = Path.Combine(volume.RequirePath(), Guid.NewGuid().ToString("N"));
        var appRoot = Path.Combine(temp, "app");
        Directory.CreateDirectory(appRoot);

        var baseline = Path.Combine(temp, "baseline.config");
        File.WriteAllText(
            baseline,
            """
            <?xml version="1.0"?>
            <configuration>
              <system.webServer>
                <security>
                  <requestFiltering>
                    <hiddenSegments><add segment="App_Data" /></hiddenSegments>
                  </requestFiltering>
                </security>
                <handlers>
                  <add name="StaticFile" path="*" verb="*" modules="StaticFileModule"
                       resourceType="Either" />
                </handlers>
              </system.webServer>
            </configuration>
            """);

        foreach (var name in new[] { "Sub", "sub" })
        {
            var directory = Path.Combine(appRoot, name);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "web.config"),
                $"""
                <?xml version="1.0"?>
                <configuration>
                  <system.webServer>
                    <handlers>
                      <add name="{name}W" path="*.aspx" verb="*" type="Probe.Handler{name}" />
                    </handlers>
                  </system.webServer>
                </configuration>
                """);
        }

        Directory.GetDirectories(appRoot).Length.ShouldBe(2);

        var failure = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(
                baseline, Path.Combine(appRoot, "web.config"), "/app"));

        failure.Message.ShouldContain("/app/Sub");
        failure.Message.ShouldContain("/app/sub");
    }
}
