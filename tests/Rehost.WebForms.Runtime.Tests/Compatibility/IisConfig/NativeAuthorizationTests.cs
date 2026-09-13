using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

public sealed class NativeAuthorizationTests : IDisposable
{
    private const string Section = "<authorization> under <system.webServer><security>";

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-nativeauth-");

    public void Dispose() => _root.Delete(recursive: true);

    [Theory]
    [InlineData("<authorization />")]
    [InlineData("""<authorization><add accessType="Allow" users="*" /></authorization>""")]
    public void A_Native_Authorization_Section_Fails_Activation(string authorization)
    {
        var app = WriteApplication($"<security>{authorization}</security>");

        var failure = RefusalMessage(app);

        failure.ShouldContain(Section, Case.Sensitive);
        failure.ShouldNotContain("inside <location");
        failure.ShouldContain("does not enforce", Case.Sensitive);
        failure.ShouldContain("<authorization> under <system.web> in that folder's", Case.Sensitive);
    }

    [Fact]
    public void An_Access_Policy_On_The_Handlers_Section_Fails_Activation()
    {
        var app = WriteApplication("""<handlers accessPolicy="Read" />""");

        RefusalMessage(app).ShouldContain("""<handlers accessPolicy="Read">""", Case.Sensitive);
    }

    [Theory]
    [InlineData("<security><authorization /></security>", Section)]
    [InlineData("""<handlers accessPolicy="None" />""", """<handlers accessPolicy="None">""")]
    public void A_Folder_Web_Config_Carrying_Either_Element_Fails_Activation(
        string content, string element)
    {
        var app = WriteApplication("");
        var folder = WriteFolder("uploads", content);

        RefusalMessage(app, namedFile: folder).ShouldContain(element, Case.Sensitive);
    }

    [Theory]
    [InlineData("<security><authorization /></security>", Section)]
    [InlineData("""<handlers accessPolicy="Execute" />""", """<handlers accessPolicy="Execute">""")]
    public void Either_Element_Inside_A_Location_Block_Fails_Activation(
        string content, string element)
    {
        var app = WriteConfig(
            _root.FullName,
            $"""
            <location path="uploads">
            <system.webServer>
            {content}
            </system.webServer>
            </location>
            """);

        var failure = RefusalMessage(app);

        failure.ShouldContain(element, Case.Sensitive);
        failure.ShouldContain("""inside <location path="uploads">""", Case.Sensitive);
    }

    [Fact]
    public void Managed_Authorization_And_A_Policy_Free_Handlers_Section_Load()
    {
        var app = WriteConfig(
            _root.FullName,
            """
            <system.web>
            <authorization><deny users="?" /></authorization>
            </system.web>
            """);
        WriteFolder(
            "uploads",
            """<handlers><add name="Probe" path="*.probe" verb="*" type="Probe.Handler" /></handlers>""");

        Should.NotThrow(() => IisServerConfiguration.Load(ShippedBaseline, app));
    }

    private static string ShippedBaseline => Path.Combine(
        AppContext.BaseDirectory, "configs", "rehost-webforms.applicationHost.config");

    private static string RefusalMessage(string applicationConfigPath, string? namedFile = null)
    {
        var failure = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(ShippedBaseline, applicationConfigPath));

        failure.Message.ShouldContain(namedFile ?? applicationConfigPath);
        return failure.Message;
    }

    private string WriteApplication(string systemWebServerContent) => WriteConfig(
        _root.FullName,
        $"""
        <system.webServer>
        {systemWebServerContent}
        </system.webServer>
        """);

    private string WriteFolder(string folder, string systemWebServerContent) => WriteConfig(
        Directory.CreateDirectory(Path.Combine(_root.FullName, folder)).FullName,
        $"""
        <system.webServer>
        {systemWebServerContent}
        </system.webServer>
        """);

    private static string WriteConfig(string directory, string configurationContent)
    {
        var path = Path.Combine(directory, "web.config");
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
