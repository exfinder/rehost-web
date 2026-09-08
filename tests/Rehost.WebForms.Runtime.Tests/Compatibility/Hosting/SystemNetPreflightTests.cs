using Rehost.WebForms.Hosting;
using Rehost.WebForms.Runtime.Tests.Compatibility.Diagnostics;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Hosting;

// Framework's machine.config declares system.net and its BCL read it; .NET's reads none of it,
// so it is declared for activation and reported once.
[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class SystemNetPreflightTests
{
    [Fact]
    public void An_Application_Carrying_System_Net_Activates_And_Is_Told_Once()
    {
        using var application = TemporaryApplication.Create();
        WriteWebConfig(application, """
            <system.net>
              <defaultProxy enabled="false" />
              <connectionManagement><add address="*" maxconnection="100" /></connectionManagement>
              <mailSettings>
                <smtp deliveryMethod="Network" from="forum@example.invalid">
                  <network host="smtp.example.invalid" port="587" />
                </smtp>
              </mailSettings>
            </system.net>
            """);
        using var listener = new RuntimeEventCollector();

        Should.NotThrow(() => ApplicationConfigurationPreflight.Validate(application.CreateConfiguration()));

        var report = listener.EventsWithId(10).ShouldHaveSingleItem();
        report.Payload.ShouldHaveSingleItem().ShouldEndWith("web.config");
    }

    [Fact]
    public void An_Application_Without_System_Net_Is_Not_Told()
    {
        using var application = TemporaryApplication.Create();
        WriteWebConfig(application, "");
        using var listener = new RuntimeEventCollector();

        ApplicationConfigurationPreflight.Validate(application.CreateConfiguration());

        listener.EventsWithId(10).ShouldBeEmpty();
    }

    private static void WriteWebConfig(TemporaryApplication application, string sections)
    {
        File.WriteAllText(
            Path.Combine(application.PhysicalRoot.FullName, "web.config"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              {sections}
              <system.web>
                <compilation targetFramework="4.8" />
              </system.web>
            </configuration>
            """);
    }
}
