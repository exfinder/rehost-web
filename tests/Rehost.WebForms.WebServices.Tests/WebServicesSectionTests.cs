using System.Configuration;
using System.Web.Services.Configuration;

using Shouldly;
using Xunit;

namespace Rehost.WebForms.WebServices.Tests;

public sealed class WebServicesSectionTests
{
    [Fact]
    public void Compatibility_Types_Use_Project_Assembly_Name()
    {
        typeof(WebServicesSection).Assembly.GetName().Name
            .ShouldBe("Rehost.WebForms.WebServices");
    }

    [Fact]
    public void Configuration_Materializes_Enabled_Protocols()
    {
        string configurationPath = Path.Combine(Path.GetTempPath(), $"rehost-web-services-{Guid.NewGuid():N}.config");
        try
        {
            File.WriteAllText(configurationPath, $$"""
                <?xml version="1.0" encoding="utf-8" ?>
                <configuration>
                  <configSections>
                    <sectionGroup name="system.web">
                      <section name="webServices" type="{{typeof(WebServicesSection).AssemblyQualifiedName}}" />
                    </sectionGroup>
                  </configSections>
                  <system.web>
                    <webServices>
                      <protocols>
                        <add name="HttpGet" />
                        <add name="HttpPost" />
                      </protocols>
                    </webServices>
                  </system.web>
                </configuration>
                """);
            ExeConfigurationFileMap map = new() { ExeConfigFilename = configurationPath };
            Configuration configuration = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);

            WebServicesSection section = (WebServicesSection)configuration.GetSection("system.web/webServices");

            section.Protocols.Count.ShouldBe(2);
            section.EnabledProtocols.ShouldBe(WebServiceProtocols.HttpGet | WebServiceProtocols.HttpPost);
        }
        finally
        {
            File.Delete(configurationPath);
        }
    }

    [Fact]
    public void Empty_Configuration_Has_No_Implicit_Protocols_In_Initial_Profile()
    {
        WebServicesSection section = new();

        section.Protocols.Count.ShouldBe(0);
        section.EnabledProtocols.ShouldBe(WebServiceProtocols.Unknown);
    }
}
