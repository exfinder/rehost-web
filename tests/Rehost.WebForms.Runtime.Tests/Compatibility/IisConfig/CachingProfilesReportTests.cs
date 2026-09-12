using System.Web.IisConfig;
using Rehost.WebForms.Hosting;
using Rehost.WebForms.Runtime.Tests.Compatibility.Diagnostics;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// IIS answered a profiled managed URL from a stored copy without running the handler (CP16, CP17);
// this host runs every request, so the profiles that changed what the application executed are
// reported once. A static extension changes only the header word, which is honored.
[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class CachingProfilesReportTests
{
    [Fact]
    public void Profiles_On_Managed_Extensions_Are_Reported_Once()
    {
        using var application = TemporaryApplication.Create();
        WriteWebConfig(application, """
            <add extension=".aspx" policy="CacheForTimePeriod" duration="00:00:05" />
            <add extension=".css" policy="CacheUntilChange" location="Client" />
            <add extension=".axd" kernelCachePolicy="CacheForTimePeriod" />
            """);
        using var listener = new RuntimeEventCollector();

        Report(application);

        var report = listener.EventsWithId(11).ShouldHaveSingleItem();
        report.Payload.Count.ShouldBe(2);
        report.Payload[0].ShouldEndWith("web.config");
        report.Payload[1].ShouldBe(".aspx");
    }

    [Fact]
    public void Profiles_On_Static_Extensions_Are_Not_Reported()
    {
        using var application = TemporaryApplication.Create();
        WriteWebConfig(application, """
            <add extension=".css" policy="CacheUntilChange" location="Client" />
            <add extension=".axd" kernelCachePolicy="CacheForTimePeriod" />
            """);
        using var listener = new RuntimeEventCollector();

        Report(application);

        listener.EventsWithId(11).ShouldBeEmpty();
    }

    // A path-specific row does not serve probe.axd, so the extension stays static (MH28).
    [Fact]
    public void A_Path_Specific_Managed_Row_Does_Not_Make_Its_Extension_Dynamic()
    {
        using var application = TemporaryApplication.Create();
        WriteWebConfig(application, """
            <add extension=".axd" policy="CacheForTimePeriod" duration="00:00:05" />
            <add extension=".ashx" policy="CacheForTimePeriod" duration="00:00:05" />
            """);
        using var listener = new RuntimeEventCollector();

        Report(application);

        var report = listener.EventsWithId(11).ShouldHaveSingleItem();
        report.Payload[1].ShouldBe(".ashx");
    }

    private static void Report(TemporaryApplication application)
    {
        var configuration = application.CreateConfiguration();
        var serverConfiguration = IisServerConfiguration.Load(
            configuration.ServerConfigurationFilePath,
            configuration.ApplicationConfigurationFilePath,
            configuration.VirtualRootPath);

        serverConfiguration.ReportUnsupported(configuration.ApplicationConfigurationFilePath);
    }

    private static void WriteWebConfig(TemporaryApplication application, string profiles)
    {
        File.WriteAllText(
            Path.Combine(application.PhysicalRoot.FullName, "web.config"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <system.webServer>
                <caching>
                  <profiles>
                    {profiles}
                  </profiles>
                </caching>
              </system.webServer>
            </configuration>
            """);
    }
}
