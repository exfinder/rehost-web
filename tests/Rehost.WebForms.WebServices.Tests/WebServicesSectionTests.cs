using System.Configuration;
using System.Web.Services.Configuration;

using Shouldly;
using Xunit;

namespace Rehost.WebForms.WebServices.Tests;

public sealed class WebServicesSectionTests
{
    [Fact]
    public void Configuration_materializes_enabled_protocols()
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
    public void Empty_configuration_has_no_implicit_protocols_in_initial_profile()
    {
        WebServicesSection section = new();

        section.Protocols.Count.ShouldBe(0);
        section.EnabledProtocols.ShouldBe(WebServiceProtocols.Unknown);
    }
}
