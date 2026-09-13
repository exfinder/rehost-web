using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The element as an application receives it: the schema defaults from the shipped baseline, then
// the application's own attributes on top, and the header shapes IIS's native static module put
// on the wire for them.
public sealed class ClientCacheTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-clientcache-");

    public void Dispose() => _root.Delete(recursive: true);

    public enum Placement
    {
        Root,
        Location,
        Folder,
    }

    [Fact]
    public void An_Application_Without_The_Element_Inherits_The_Baseline_Defaults_And_Sends_No_Header()
    {
        var clientCache = Load("");

        clientCache.Mode.ShouldBe(ClientCacheMode.NoControl);
        clientCache.MaxAge.ShouldBe(TimeSpan.FromDays(1));
        clientCache.CacheControl(null).ShouldBeNull();
        clientCache.CacheControl("public").ShouldBe("public");
        clientCache.Expires.ShouldBeNull();
    }

    [Fact]
    public void An_Application_Amends_The_Element_Beside_Its_Own_Mime_Maps()
    {
        var configuration = IisServerConfiguration.Load(
            ShippedBaseline,
            WriteApplication(
                """
                <staticContent>
                  <clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="00:00:30" cacheControlCustom="public" />
                  <mimeMap fileExtension=".probe" mimeType="text/probe" />
                </staticContent>
                """));

        configuration.ClientCache.Mode.ShouldBe(ClientCacheMode.UseMaxAge);
        configuration.ClientCache.MaxAge.ShouldBe(TimeSpan.FromSeconds(30));
        configuration.ClientCache.CacheControl(null).ShouldBe("public,max-age=30");
        configuration.StaticContentTypeOf(".probe").ShouldBe("text/probe");
        configuration.StaticContentTypeOf(".json").ShouldBe("application/json");
    }

    [Theory]
    [InlineData("", null, null, null)]
    [InlineData(
        """<clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="1.00:00:00" />""",
        null,
        "max-age=86400",
        null)]
    [InlineData(
        """<clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="00:00:30" cacheControlCustom="public" />""",
        null,
        "public,max-age=30",
        null)]
    [InlineData(
        """<clientCache cacheControlMode="UseMaxAge" cacheControlCustom="private" httpExpires="Tue, 19 Jan 2038 03:14:07 GMT" />""",
        null,
        "private,max-age=86400",
        null)]
    [InlineData(
        """<clientCache cacheControlMode="UseExpires" httpExpires="Tue, 19 Jan 2038 03:14:07 GMT" />""",
        null,
        null,
        "Tue, 19 Jan 2038 03:14:07 GMT")]
    [InlineData(
        """<clientCache cacheControlMode="UseExpires" httpExpires="Tue, 19 Jan 2038 03:14:07 GMT" cacheControlCustom="public" />""",
        null,
        "public",
        "Tue, 19 Jan 2038 03:14:07 GMT")]
    [InlineData("""<clientCache cacheControlMode="DisableCache" />""", null, "no-cache", null)]
    [InlineData(
        """<clientCache cacheControlCustom="public, must-revalidate" />""",
        null,
        "public, must-revalidate",
        null)]
    [InlineData(
        """<clientCache cacheControlMode="UseMaxAge" cacheControlMaxAge="1.00:00:00" />""",
        "public",
        "public,max-age=86400",
        null)]
    public void The_Headers_Carry_The_Profile_Word_Then_The_Custom_Text_Then_The_Span(
        string element, string? profileWord, string? cacheControl, string? expires)
    {
        var clientCache = Load($"<staticContent>{element}</staticContent>");

        clientCache.CacheControl(profileWord).ShouldBe(cacheControl);
        clientCache.Expires.ShouldBe(expires);
    }

    [Theory]
    [InlineData(
        Placement.Root,
        """<clientCache cacheControlMaxAge="abc" />""",
        """<clientCache cacheControlMaxAge="abc">""",
        "is not a time span")]
    [InlineData(
        Placement.Root,
        """<clientCache cacheControlMode="Forever" />""",
        """<clientCache cacheControlMode="Forever">""",
        "is not one of NoControl, UseMaxAge, UseExpires, DisableCache")]
    [InlineData(
        Placement.Root,
        """<clientCache setEtag="false" />""",
        """<clientCache setEtag="false">""",
        "writes the ETag with the response's cache headers")]
    [InlineData(
        Placement.Root,
        """<clientCache cacheControlCustom="public&#13;X-Injected: 1" />""",
        """<clientCache cacheControlCustom="public""",
        "carries a carriage return or line feed")]
    [InlineData(
        Placement.Location,
        """<clientCache cacheControlMode="UseMaxAge" />""",
        "<clientCache> inside <location>",
        "application root web.config")]
    [InlineData(
        Placement.Folder,
        """<clientCache cacheControlMode="UseMaxAge" />""",
        "<clientCache>",
        "application root web.config")]
    public void A_Value_Or_A_Scope_This_Port_Refuses_Fails_Activation(
        Placement placement, string element, string expected, string rule)
    {
        var (application, named) = Write(placement, element);

        var failure = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(ShippedBaseline, application));

        failure.Message.ShouldContain(named);
        failure.Message.ShouldContain(expected, Case.Sensitive);
        failure.Message.ShouldContain(rule, Case.Sensitive);
    }

    private static string ShippedBaseline => Path.Combine(
        AppContext.BaseDirectory, "configs", "rehost-webforms.applicationHost.config");

    private ClientCache Load(string systemWebServerContent) =>
        IisServerConfiguration.Load(ShippedBaseline, WriteApplication(systemWebServerContent))
            .ClientCache;

    private (string Application, string Named) Write(Placement placement, string element)
    {
        if (placement == Placement.Folder)
        {
            var folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "sub"));
            var folderConfig = Path.Combine(folder.FullName, "web.config");
            File.WriteAllText(
                folderConfig,
                $"""
                <?xml version="1.0"?>
                <configuration><system.webServer>
                <staticContent>{element}</staticContent>
                </system.webServer></configuration>
                """);
            return (WriteApplication(""), folderConfig);
        }

        var application = placement == Placement.Location
            ? WriteConfig(
                $"""
                <location path="sub">
                <system.webServer>
                <staticContent>{element}</staticContent>
                </system.webServer>
                </location>
                """)
            : WriteApplication($"<staticContent>{element}</staticContent>");
        return (application, application);
    }

    private string WriteApplication(string systemWebServerContent) => WriteConfig(
        $"""
        <system.webServer>
        {systemWebServerContent}
        </system.webServer>
        """);

    private string WriteConfig(string configurationContent)
    {
        var path = Path.Combine(_root.FullName, "web.config");
        File.WriteAllText(
            path,
            $"""
            <?xml version="1.0"?>
            <configuration>
            {configurationContent}
            </configuration>
            """);
        return path;
    }
}
