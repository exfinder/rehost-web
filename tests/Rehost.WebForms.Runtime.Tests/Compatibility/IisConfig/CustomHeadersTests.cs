using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The collection as an application receives it: the shipped baseline's row first, then the
// application's own in document order (CH1-CH4, CH10, CH11). The refusals are the ones IIS
// answered with a 500.19 (CH12-CH14) plus the two shapes IIS wrote raw and Kestrel cannot (CH23).
public sealed class CustomHeadersTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-headers-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void An_Application_Without_The_Section_Inherits_The_Baseline_Row()
    {
        Rows(Load("")).ShouldBe(["X-Powered-By=ASP.NET"]);
    }

    [Fact]
    public void Removing_The_Inherited_Row_Drops_It_And_Frees_The_Name()
    {
        var dropped = Load(
            """
            <httpProtocol>
              <customHeaders>
                <remove name="X-Powered-By" />
                <add name="X-Custom" value="v" />
              </customHeaders>
            </httpProtocol>
            """);

        var replaced = Load(
            """
            <httpProtocol>
              <customHeaders>
                <remove name="x-powered-by" />
                <add name="X-Custom" value="v" />
                <add name="X-Powered-By" value="mine" />
              </customHeaders>
            </httpProtocol>
            """);

        Rows(dropped).ShouldBe(["X-Custom=v"]);
        Rows(replaced).ShouldBe(["X-Custom=v", "X-Powered-By=mine"]);
    }

    [Fact]
    public void A_Clear_Empties_The_Inherited_Collection()
    {
        var headers = Load(
            """
            <httpProtocol>
              <customHeaders>
                <clear />
                <add name="X-Only" value="1" />
              </customHeaders>
            </httpProtocol>
            """);

        Rows(headers).ShouldBe(["X-Only=1"]);
    }

    [Theory]
    [InlineData("X-Custom")]
    [InlineData("x-custom")]
    public void A_Duplicate_Name_Fails_Activation(string second)
    {
        var failure = Refusal(
            $"""
            <httpProtocol>
              <customHeaders>
                <add name="X-Custom" value="one" />
                <add name="{second}" value="two" />
              </customHeaders>
            </httpProtocol>
            """);

        failure.ShouldContain($"""<add name="{second}">""", Case.Sensitive);
        failure.ShouldContain("duplicates an entry");
    }

    [Fact]
    public void Re_Adding_The_Inherited_Name_Without_Removing_It_Fails_Activation()
    {
        var failure = Refusal(
            """
            <httpProtocol>
              <customHeaders>
                <add name="X-Powered-By" value="mine" />
              </customHeaders>
            </httpProtocol>
            """);

        failure.ShouldContain("""<add name="X-Powered-By">""", Case.Sensitive);
    }

    [Fact]
    public void A_Name_Outside_The_Token_Set_Fails_Activation()
    {
        var failure = Refusal(
            """
            <httpProtocol>
              <customHeaders>
                <add name="X Bad" value="x" />
              </customHeaders>
            </httpProtocol>
            """);

        failure.ShouldContain("""<add name="X Bad">""", Case.Sensitive);
        failure.ShouldContain("is not an HTTP header name");
    }

    [Theory]
    [InlineData("a&#13;b")]
    [InlineData("a&#10;b")]
    public void A_Value_Carrying_A_Line_Break_Fails_Activation(string value)
    {
        var failure = Refusal(
            $"""
            <httpProtocol>
              <customHeaders>
                <add name="X-Split" value="{value}" />
              </customHeaders>
            </httpProtocol>
            """);

        failure.ShouldContain("""<add name="X-Split">""", Case.Sensitive);
        failure.ShouldContain("carriage return or line feed");
    }

    [Fact]
    public void A_Section_Inside_A_Location_Block_Fails_Activation()
    {
        var app = WriteConfig(
            "web.config",
            """
            <location path="sub">
              <system.webServer>
                <httpProtocol>
                  <customHeaders><add name="X-Loc" value="1" /></customHeaders>
                </httpProtocol>
              </system.webServer>
            </location>
            """);

        var failure = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(ShippedBaseline, app));

        failure.Message.ShouldContain("<customHeaders> inside <location>", Case.Sensitive);
        failure.Message.ShouldContain(app);
        failure.Message.ShouldContain("application root web.config");
    }

    [Fact]
    public void A_Folder_Web_Config_Carrying_The_Section_Fails_Activation()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "sub"));
        var folderConfig = Path.Combine(folder.FullName, "web.config");
        File.WriteAllText(
            folderConfig,
            """
            <?xml version="1.0"?>
            <configuration><system.webServer>
              <httpProtocol>
                <customHeaders><add name="X-Folder" value="1" /></customHeaders>
              </httpProtocol>
            </system.webServer></configuration>
            """);

        var failure = Should.Throw<ConfigurationErrorsException>(() => Load(""));

        failure.Message.ShouldContain("<customHeaders>", Case.Sensitive);
        failure.Message.ShouldContain(folderConfig);
        failure.Message.ShouldContain("application root web.config");
    }

    [Fact]
    public void Removing_Server_Is_Accepted_And_Only_RequestFiltering_Switches_It_Off()
    {
        var inert = Load(
            """
            <httpProtocol>
              <customHeaders>
                <remove name="Server" />
              </customHeaders>
            </httpProtocol>
            """);

        var switched = Load(
            """
            <security>
              <requestFiltering removeServerHeader="true" />
            </security>
            """);

        Rows(inert).ShouldBe(["X-Powered-By=ASP.NET"]);
        inert.RemoveServerHeader.ShouldBeFalse();
        switched.RemoveServerHeader.ShouldBeTrue();
    }

    private static string ShippedBaseline => Path.Combine(
        AppContext.BaseDirectory, "configs", "rehost-webforms.applicationHost.config");

    private static string[] Rows(CustomHeaders headers) =>
        headers.Rows.Select(row => $"{row.Name}={row.Value}").ToArray();

    private CustomHeaders Load(string systemWebServerContent) =>
        IisServerConfiguration.Load(ShippedBaseline, WriteApplication(systemWebServerContent))
            .CustomHeaders;

    private string Refusal(string systemWebServerContent)
    {
        var app = WriteApplication(systemWebServerContent);
        var failure = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(ShippedBaseline, app));

        failure.Message.ShouldContain(app);
        return failure.Message;
    }

    private string WriteApplication(string systemWebServerContent) => WriteConfig(
        "web.config",
        $"""
        <system.webServer>
        {systemWebServerContent}
        </system.webServer>
        """);

    private string WriteConfig(string name, string configurationContent)
    {
        var path = Path.Combine(_root.FullName, name);
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
