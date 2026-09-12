using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The section the host parses is carried, and the shapes the importer would accept and then drop
// in silence stop activation instead, as IIS answered them with a 500 (readings UR24, UR25, UR54).
public sealed class RewriteSectionTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-rewrite-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void A_Well_Formed_Section_Is_Carried_With_The_File_It_Came_From()
    {
        var app = WriteApplication(
            """
            <rewrite>
              <rules>
                <rule name="clean">
                  <match url="^clean/(.*)$" />
                  <action type="Rewrite" url="probe.aspx?id={R:1}" />
                </rule>
              </rules>
            </rewrite>
            """);

        var rewrite = IisServerConfiguration.Load(Baseline(), app).Rewrite;

        rewrite.ShouldNotBeNull();
        rewrite.ConfigPath.ShouldBe(app);
        rewrite.Xml.ShouldContain("""<action type="Rewrite" url="probe.aspx?id={R:1}" />""", Case.Sensitive);
        rewrite.Xml.ShouldStartWith("<rewrite", Case.Sensitive);
    }

    [Fact]
    public void An_Application_Without_The_Section_Carries_Nothing()
    {
        IisServerConfiguration.Load(Baseline(), WriteApplication("")).Rewrite.ShouldBeNull();
    }

    [Fact]
    public void Rules_Inside_A_Location_Block_Fail_Activation()
    {
        var app = WriteConfig(
            "web.config",
            """
            <location path="sub">
              <system.webServer>
                <rewrite><rules><rule name="l"><match url="^a$" /><action type="Rewrite" url="probe.aspx" /></rule></rules></rewrite>
              </system.webServer>
            </location>
            """);

        Refusal(app).ShouldContain("<rewrite> inside <location>", Case.Sensitive);
    }

    [Fact]
    public void A_Folder_Web_Config_Carrying_The_Section_Fails_Activation()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root.FullName, "sub"));
        File.WriteAllText(
            Path.Combine(folder.FullName, "web.config"),
            """
            <?xml version="1.0"?>
            <configuration><system.webServer>
              <rewrite><rules><rule name="f"><match url="^a$" /><action type="Rewrite" url="probe.aspx" /></rule></rules></rewrite>
            </system.webServer></configuration>
            """);

        var failure = Should.Throw<InvalidOperationException>(
            () => IisServerConfiguration.Load(Baseline(), WriteApplication("")));

        failure.Message.ShouldContain("<rewrite>", Case.Sensitive);
        failure.Message.ShouldContain(Path.Combine(folder.FullName, "web.config"));
        failure.Message.ShouldContain("application root web.config");
    }

    [Fact]
    public void Global_Rules_Fail_Activation()
    {
        var app = WriteApplication(
            """
            <rewrite>
              <globalRules><rule name="g"><match url="^a$" /><action type="Rewrite" url="probe.aspx" /></rule></globalRules>
            </rewrite>
            """);

        Refusal(app).ShouldContain("<globalRules>", Case.Sensitive);
    }

    [Fact]
    public void Outbound_Rules_Fail_Activation()
    {
        var app = WriteApplication(
            """
            <rewrite>
              <outboundRules>
                <rule name="o"><match filterByTags="None" pattern="REWRITE_ME" /><action type="Rewrite" value="done" /></rule>
              </outboundRules>
            </rewrite>
            """);

        Refusal(app).ShouldContain("<outboundRules>", Case.Sensitive);
    }

    [Fact]
    public void Setting_A_Server_Variable_Fails_Activation()
    {
        var app = WriteApplication(
            """
            <rewrite>
              <rules>
                <rule name="v">
                  <match url="^a$" />
                  <serverVariables><set name="HTTP_X_FOO" value="foo-set" /></serverVariables>
                  <action type="Rewrite" url="probe.aspx" />
                </rule>
              </rules>
            </rewrite>
            """);

        var failure = Refusal(app);

        failure.ShouldContain("<serverVariables>", Case.Sensitive);
        failure.ShouldContain("allowedServerVariables", Case.Sensitive);
    }

    [Fact]
    public void Wildcard_Pattern_Syntax_Fails_Activation()
    {
        var app = WriteApplication(
            """
            <rewrite>
              <rules>
                <rule name="w" patternSyntax="Wildcard"><match url="w/*" /><action type="Rewrite" url="probe.aspx" /></rule>
              </rules>
            </rewrite>
            """);

        Refusal(app).ShouldContain("""patternSyntax="Wildcard""", Case.Sensitive);
    }

    [Fact]
    public void A_Substatus_Code_Fails_Activation()
    {
        var app = WriteApplication(
            """
            <rewrite>
              <rules>
                <rule name="s">
                  <match url="^a$" />
                  <action type="CustomResponse" statusCode="403" subStatusCode="7" statusReason="Nope" />
                </rule>
              </rules>
            </rewrite>
            """);

        Refusal(app).ShouldContain("subStatusCode", Case.Sensitive);
    }

    [Fact]
    public void A_Repeated_Rule_Name_Fails_Activation()
    {
        var app = WriteApplication(
            """
            <rewrite>
              <rules>
                <rule name="dup"><match url="^a$" /><action type="Rewrite" url="probe.aspx?a=1" /></rule>
                <rule name="DUP"><match url="^b$" /><action type="Rewrite" url="probe.aspx?b=1" /></rule>
              </rules>
            </rewrite>
            """);

        Refusal(app).ShouldContain("""<rule name="DUP">""", Case.Sensitive);
    }

    [Theory]
    [InlineData("/other/probe.aspx")]
    [InlineData("../other/probe.aspx")]
    [InlineData("http://example.invalid/probe.aspx")]
    public void A_Target_Outside_The_Application_Fails_Activation(string url)
    {
        var app = WriteApplication(
            $"""
            <rewrite>
              <rules>
                <rule name="out"><match url="^a$" /><action type="Rewrite" url="{url}" /></rule>
              </rules>
            </rewrite>
            """);

        var failure = Refusal(app);

        failure.ShouldContain(url, Case.Sensitive);
        failure.ShouldContain("leaves the application");
    }

    [Fact]
    public void A_Substitution_That_Only_Looks_Absolute_At_Request_Time_Loads()
    {
        var app = WriteApplication(
            """
            <rewrite>
              <rules>
                <rule name="late"><match url="^a/(.*)$" /><action type="Rewrite" url="{R:1}" /></rule>
              </rules>
            </rewrite>
            """);

        IisServerConfiguration.Load(Baseline(), app).Rewrite.ShouldNotBeNull();
    }

    private string Refusal(string applicationConfigPath)
    {
        var failure = Should.Throw<InvalidOperationException>(
            () => IisServerConfiguration.Load(Baseline(), applicationConfigPath));

        failure.Message.ShouldContain(applicationConfigPath);
        return failure.Message;
    }

    private string Baseline() => WriteConfig(
        "baseline.config",
        """
        <system.webServer>
          <staticContent><mimeMap fileExtension=".css" mimeType="text/css" /></staticContent>
        </system.webServer>
        """);

    private string WriteApplication(string systemWebServerContent) => WriteConfig(
        "web.config",
        systemWebServerContent.Length == 0
            ? "<system.webServer />"
            : "<system.webServer>" + systemWebServerContent + "</system.webServer>");

    private string WriteConfig(string name, string configurationContent)
    {
        var path = Path.Combine(_root.FullName, name);
        File.WriteAllText(
            path,
            """<?xml version="1.0"?><configuration>""" + configurationContent + "</configuration>");
        return path;
    }
}
