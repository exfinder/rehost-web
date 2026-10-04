using System.Configuration;
using System.Web;
using System.Web.IisConfig;
using Rehost.Web.Hosting;
using Rehost.Web.Tests.Compatibility.Diagnostics;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.IisConfig;

// IIS applied every <system.webServer> element an application set, so one this port has no reader
// for is behavior the application still expects. Compression is the exception: nothing here
// compresses, so the sections are reported rather than refused.
[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class UnhonoredSectionsTests
{
    public enum Placement
    {
        Root,
        Location,
        Folder,
    }

    private const string InsideLocation = """<system.webServer> inside <location path="Temp">""";

    [Theory]
    [InlineData(Placement.Root, """<directoryBrowse enabled="true" />""", "<directoryBrowse>")]
    [InlineData(
        Placement.Root,
        """<security><authentication><anonymousAuthentication enabled="false" /></authentication></security>""",
        "<security/authentication>")]
    [InlineData(
        Placement.Root,
        """
        <httpProtocol>
          <customHeaders><add name="X-Kept" value="1" /></customHeaders>
          <redirectHeaders><add name="X-Moved" value="1" /></redirectHeaders>
        </httpProtocol>
        """,
        "<httpProtocol/redirectHeaders>")]
    [InlineData(Placement.Location, """<handlers><clear /></handlers>""", InsideLocation)]
    [InlineData(
        Placement.Location,
        """<httpProtocol><customHeaders><add name="X-Loc" value="1" /></customHeaders></httpProtocol>""",
        InsideLocation)]
    [InlineData(
        Placement.Location,
        """<httpErrors><error statusCode="410" path="err.htm" /></httpErrors>""",
        InsideLocation)]
    [InlineData(
        Placement.Location,
        """<staticContent><clientCache cacheControlMode="UseMaxAge" /></staticContent>""",
        InsideLocation)]
    [InlineData(
        Placement.Location,
        """<rewrite><rules><rule name="l"><match url="^a$" /><action type="Rewrite" url="probe.aspx" /></rule></rules></rewrite>""",
        InsideLocation)]
    [InlineData(
        Placement.Folder,
        """<staticContent><mimeMap fileExtension=".probe" mimeType="text/probe" /></staticContent>""",
        "<staticContent/mimeMap>")]
    [InlineData(
        Placement.Folder,
        """<httpProtocol><customHeaders><add name="X-Folder" value="1" /></customHeaders></httpProtocol>""",
        "<httpProtocol>")]
    [InlineData(
        Placement.Folder,
        """<httpErrors><error statusCode="410" path="err.htm" /></httpErrors>""",
        "<httpErrors>")]
    [InlineData(
        Placement.Folder,
        """<rewrite><rules><rule name="f"><match url="^a$" /><action type="Rewrite" url="probe.aspx" /></rule></rules></rewrite>""",
        "<rewrite>")]
    public void A_Section_This_Port_Does_Not_Honor_Fails_Activation(
        Placement placement, string content, string element)
    {
        using var application = TemporaryApplication.Create();
        var named = Write(application, placement, content);

        var failure = Should.Throw<ConfigurationErrorsException>(
            () => Load(application.CreateConfiguration()));

        failure.Message.ShouldContain(named);
        failure.Message.ShouldContain(element, Case.Sensitive);
    }

    [Fact]
    public void A_Folder_File_Carrying_ClientCache_Loads_Clean()
    {
        using var application = TemporaryApplication.Create();
        Write(
            application,
            Placement.Folder,
            """<staticContent><clientCache cacheControlMode="DisableCache" /></staticContent>""");

        var server = Load(application.CreateConfiguration());

        server.ClientCacheFor(VirtualPath.Create("/Temp/a.txt")).Mode
            .ShouldBe(ClientCacheMode.DisableCache);
    }

    [Fact]
    public void A_Root_File_Carrying_Every_Honored_Section_Loads_Clean()
    {
        using var application = TemporaryApplication.Create();
        WriteRoot(
            application,
            """
            <validation validateIntegratedModeConfiguration="false" />
            <defaultDocument><files><add value="probe.aspx" /></files></defaultDocument>
            <modules><add name="ProbeModule" type="Probe.Module" /></modules>
            <handlers><add name="Probe" path="*.probe" verb="*" type="Probe.Handler" /></handlers>
            <httpErrors errorMode="Custom"><error statusCode="410" path="/gone" responseMode="Redirect" /></httpErrors>
            <rewrite><rules><rule name="clean"><match url="^clean/(.*)$" /><action type="Rewrite" url="probe.aspx?id={R:1}" /></rule></rules></rewrite>
            <staticContent>
              <clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="00:00:30" />
              <mimeMap fileExtension=".probe" mimeType="text/probe" />
            </staticContent>
            <httpProtocol><customHeaders><add name="X-Probe" value="1" /></customHeaders></httpProtocol>
            <caching><profiles><add extension=".css" policy="CacheUntilChange" location="Client" /></profiles></caching>
            <security>
              <requestFiltering removeServerHeader="true">
                <fileExtensions allowUnlisted="true"><add fileExtension=".bad" allowed="false" /></fileExtensions>
                <hiddenSegments><add segment="probeseg" /></hiddenSegments>
                <requestLimits maxAllowedContentLength="1000" />
                <verbs allowUnlisted="true"><add verb="TRACE" allowed="false" /></verbs>
              </requestFiltering>
            </security>
            """);
        using var listener = new RuntimeEventCollector();

        var configuration = application.CreateConfiguration();
        var server = Load(configuration);
        server.ReportUnsupported(configuration.ApplicationConfigurationFilePath);

        server.RequestLimits.AllowsVerb("TRACE").ShouldBeFalse();
        server.StaticContentTypeOf(".probe").ShouldBe("text/probe");
        listener.EventsWithId(12).ShouldBeEmpty();
    }

    [Fact]
    public void The_Compression_Sections_Load_And_Are_Reported_Once_Each()
    {
        using var application = TemporaryApplication.Create();
        WriteRoot(
            application,
            """
            <urlCompression doStaticCompression="true" doDynamicCompression="true" />
            <httpCompression>
              <dynamicTypes><add mimeType="text/*" enabled="true" /></dynamicTypes>
            </httpCompression>
            """);
        using var listener = new RuntimeEventCollector();

        var configuration = application.CreateConfiguration();
        Load(configuration).ReportUnsupported(configuration.ApplicationConfigurationFilePath);

        var reports = listener.EventsWithId(12);
        reports.Count.ShouldBe(2);
        reports[0].Payload[0].ShouldBe(configuration.ApplicationConfigurationFilePath);
        reports[0].Payload[1].ShouldBe("urlCompression");
        reports[0].Payload[2].ShouldContain("does not compress responses");
        reports[1].Payload[1].ShouldBe("httpCompression");
    }

    private static IisServerConfiguration Load(ApplicationBootstrapConfiguration configuration) =>
        IisServerConfiguration.Load(
            configuration.ServerConfigurationFilePath,
            configuration.ApplicationConfigurationFilePath,
            configuration.VirtualRootPath);

    private static string Write(
        TemporaryApplication application, Placement placement, string content)
    {
        if (placement == Placement.Folder)
        {
            WriteRoot(application, "");
            var folder = Directory.CreateDirectory(
                Path.Combine(application.PhysicalRoot.FullName, "Temp"));
            return WriteFile(Path.Combine(folder.FullName, "web.config"), $"""
                <system.webServer>
                {content}
                </system.webServer>
                """);
        }

        if (placement == Placement.Location)
        {
            return WriteFile(RootPath(application), $"""
                <location path="Temp">
                <system.webServer>
                {content}
                </system.webServer>
                </location>
                """);
        }

        return WriteRoot(application, content);
    }

    private static string WriteRoot(TemporaryApplication application, string content) =>
        WriteFile(RootPath(application), $"""
            <system.webServer>
            {content}
            </system.webServer>
            """);

    private static string RootPath(TemporaryApplication application) =>
        Path.Combine(application.PhysicalRoot.FullName, "web.config");

    private static string WriteFile(string path, string configurationContent)
    {
        File.WriteAllText(
            path,
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
            {configurationContent}
            </configuration>
            """);
        return path;
    }
}
