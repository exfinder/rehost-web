using System.Configuration;
using System.Web.Services;
using System.Web.Services.Configuration;

using Shouldly;
using Xunit;

namespace Rehost.Web.Services.Tests;

public sealed class WebServicesSectionTests
{
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

            section.Protocols.Count.ShouldBe(6);
            section.EnabledProtocols.ShouldBe(
                WebServiceProtocols.HttpSoap | WebServiceProtocols.HttpSoap12 |
                WebServiceProtocols.HttpPostLocalhost | WebServiceProtocols.Documentation |
                WebServiceProtocols.HttpGet | WebServiceProtocols.HttpPost);
        }
        finally
        {
            File.Delete(configurationPath);
        }
    }

    [Fact]
    public void Empty_Configuration_Enables_Framework_Default_Protocols()
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
                    <webServices />
                  </system.web>
                </configuration>
                """);
            ExeConfigurationFileMap map = new() { ExeConfigFilename = configurationPath };
            Configuration configuration = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);

            WebServicesSection section = (WebServicesSection)configuration.GetSection("system.web/webServices");

            section.EnabledProtocols.ShouldBe(
                WebServiceProtocols.HttpSoap | WebServiceProtocols.HttpSoap12 |
                WebServiceProtocols.HttpPostLocalhost | WebServiceProtocols.Documentation);
            section.ConformanceWarnings.Cast<WsiProfilesElement>()
                .ShouldContain(element => element.Name == WsiProfiles.BasicProfile1_1);
        }
        finally
        {
            File.Delete(configurationPath);
        }
    }
}
