using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The limits as an application receives them: IIS's own numbers from the shipped baseline, then
// the application's amendment on top.
public sealed class RequestLimitsTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-limits-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void An_Application_Without_The_Section_Inherits_The_Baseline_Numbers()
    {
        var limits = Load("");

        limits.MaxAllowedContentLength.ShouldBe(30000000);
        limits.MaxUrl.ShouldBe(4096);
        limits.MaxQueryString.ShouldBe(2048);
        limits.AllowUnlistedVerbs.ShouldBeTrue();
        limits.AllowsVerb("DELETE").ShouldBeTrue();
    }

    [Fact]
    public void An_Application_Amends_The_Numbers_And_The_Verbs_Exactly()
    {
        var denied = Load(
            """
            <security>
              <requestFiltering>
                <requestLimits maxAllowedContentLength="1000" maxUrl="300" />
                <verbs>
                  <add verb="DELETE" allowed="false" />
                </verbs>
              </requestFiltering>
            </security>
            """);

        var allowList = Load(
            """
            <security>
              <requestFiltering>
                <verbs allowUnlisted="false">
                  <add verb="GET" allowed="true" />
                </verbs>
              </requestFiltering>
            </security>
            """);

        denied.MaxAllowedContentLength.ShouldBe(1000);
        denied.MaxUrl.ShouldBe(300);
        denied.MaxQueryString.ShouldBe(2048);
        denied.AllowsVerb("DELETE").ShouldBeFalse();
        denied.AllowsVerb("delete").ShouldBeTrue();
        denied.AllowsVerb("PUT").ShouldBeTrue();
        allowList.AllowsVerb("GET").ShouldBeTrue();
        allowList.AllowsVerb("get").ShouldBeFalse();
        allowList.AllowsVerb("HEAD").ShouldBeFalse();
    }

    [Theory]
    [InlineData("maxAllowedContentLength", "lots")]
    [InlineData("maxUrl", "-1")]
    public void A_Number_IIS_Would_Refuse_Fails_Activation(string attribute, string value)
    {
        var app = WriteApplication(
            $"""
            <security>
              <requestFiltering>
                <requestLimits {attribute}="{value}" />
              </requestFiltering>
            </security>
            """);

        var failure = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(ShippedBaseline, app));

        failure.Message.ShouldContain($"""<requestLimits {attribute}="{value}">""", Case.Sensitive);
        failure.Message.ShouldContain(app);
        failure.Message.ShouldContain("non-negative whole number");
    }

    private static string ShippedBaseline => Path.Combine(
        AppContext.BaseDirectory, "configs", "rehost-webforms.applicationHost.config");

    private RequestLimits Load(string systemWebServerContent) =>
        IisServerConfiguration.Load(ShippedBaseline, WriteApplication(systemWebServerContent))
            .RequestLimits;

    private string WriteApplication(string systemWebServerContent)
    {
        var path = Path.Combine(_root.FullName, "web.config");
        File.WriteAllText(
            path,
            $"""
            <?xml version="1.0"?>
            <configuration>
            <system.webServer>
            {systemWebServerContent}
            </system.webServer>
            </configuration>
            """);
        return path;
    }
}
