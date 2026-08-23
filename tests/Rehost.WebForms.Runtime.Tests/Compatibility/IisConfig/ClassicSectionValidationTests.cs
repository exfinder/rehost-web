using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The measured ConfigurationValidationModule rule (readings MH5, MH6, MH23): app-level classic
// registration content or impersonation is refused unless the application waives the check.
public sealed class ClassicSectionValidationTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-iisvalid-");

    public void Dispose() => _root.Delete(recursive: true);

    private string WriteConfig(string name, string configurationContent)
    {
        var path = Path.Combine(_root.FullName, name);
        File.WriteAllText(
            path,
            """<?xml version="1.0"?><configuration>""" + configurationContent + "</configuration>");
        return path;
    }

    private string Baseline() => WriteConfig(
        "baseline.config",
        """
        <system.webServer>
          <modules>
            <add name="Session" type="Base.SessionStateModule" preCondition="managedHandler" />
          </modules>
          <handlers>
            <add name="StaticFile" path="*" verb="*" modules="StaticFileModule" />
          </handlers>
        </system.webServer>
        """);

    private static string Waiver =>
        """<system.webServer><validation validateIntegratedModeConfiguration="false" /></system.webServer>""";

    // Reading MH5a: 500.22 for every request, static included.
    [Fact]
    public void An_App_HttpModules_Add_Fails_Activation_Naming_The_Entry_And_The_Flag()
    {
        var app = WriteConfig(
            "web.config",
            """<system.web><httpModules><add name="LogA" type="Probe.LogA" /></httpModules></system.web>""");

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("httpModules");
        exception.Message.ShouldContain("LogA");
        exception.Message.ShouldContain("web.config");
        exception.Message.ShouldContain("validateIntegratedModeConfiguration");
    }

    // Reading MH5b: 500.23, the httpHandlers variant, whose entries are named by path.
    [Fact]
    public void An_App_HttpHandlers_Add_Fails_Activation_Naming_The_Entry_And_The_Flag()
    {
        var app = WriteConfig(
            "web.config",
            """
            <system.web>
              <httpHandlers>
                <add verb="*" path="probe3.axd" type="Probe.HandlerA" />
              </httpHandlers>
            </system.web>
            """);

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("httpHandlers");
        exception.Message.ShouldContain("probe3.axd");
        exception.Message.ShouldContain("validateIntegratedModeConfiguration");
    }

    // Reading MH5c: 500.24.
    [Fact]
    public void App_Level_Impersonation_Fails_Activation()
    {
        var app = WriteConfig(
            "web.config", """<system.web><identity impersonate="true" /></system.web>""");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("impersonate");
    }

    [Fact]
    public void Impersonation_Turned_Off_Is_Not_Classic_Content()
    {
        var app = WriteConfig(
            "web.config", """<system.web><identity impersonate="false" /></system.web>""");

        IisServerConfiguration.Load(Baseline(), app).Modules
            .Select(module => module.Name).ShouldBe(new[] { "Session" });
    }

    // Reading MH6: explicit true is the same 500.22/500.23 as the element being absent.
    [Fact]
    public void An_Explicit_True_Flag_Refuses_Exactly_As_The_Absent_Element_Does()
    {
        var app = WriteConfig(
            "web.config",
            """
            <system.web>
              <httpModules>
                <add name="LogA" type="Probe.LogA" />
              </httpModules>
            </system.web>
            <system.webServer>
              <validation validateIntegratedModeConfiguration="true" />
            </system.webServer>
            """);

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("LogA");
    }

    // Reading MH23a: a bare remove with no adds trips the same 500.22.
    [Fact]
    public void A_Bare_HttpModules_Remove_Fails_Activation_Naming_The_Entry()
    {
        var app = WriteConfig(
            "web.config",
            """<system.web><httpModules><remove name="Session" /></httpModules></system.web>""");

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("remove");
        exception.Message.ShouldContain("Session");
    }

    // Reading MH23b.
    [Fact]
    public void A_Bare_HttpModules_Clear_Fails_Activation()
    {
        var app = WriteConfig(
            "web.config", "<system.web><httpModules><clear /></httpModules></system.web>");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("clear");
    }

    [Fact]
    public void An_HttpHandlers_Clear_Fails_Activation()
    {
        var app = WriteConfig(
            "web.config", "<system.web><httpHandlers><clear /></httpHandlers></system.web>");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("httpHandlers");
    }

    // Readings MH3, MH4, MH7, MH16: with the flag false the sections are dead text, and the
    // webServer lists publish untouched.
    [Fact]
    public void The_Waiver_Makes_The_Classic_Sections_Dead_Text()
    {
        var app = WriteConfig(
            "web.config",
            """
            <system.web>
              <httpModules>
                <clear />
                <add name="LogA" type="Probe.LogA" />
              </httpModules>
              <httpHandlers>
                <add verb="*" path="probe3.axd" type="Probe.HandlerA" />
              </httpHandlers>
              <identity impersonate="true" />
            </system.web>
            """
            + Waiver);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        configuration.Modules.Select(module => module.Name).ShouldBe(new[] { "Session" });
        configuration.Handlers.Select(handler => handler.Name).ShouldBe(new[] { "StaticFile" });
    }

    // The shipped classic defaults are inherited, not authored by the application, so they are
    // exempt while they still exist.
    [Fact]
    public void Classic_Content_In_The_Shipped_Baseline_Never_Trips_The_Rule()
    {
        var baseline = WriteConfig(
            "baseline.config",
            """
            <system.web>
              <httpModules>
                <add name="Session" type="Base.SessionStateModule" />
              </httpModules>
            </system.web>
            <system.webServer>
              <modules>
                <add name="Session" type="Base.SessionStateModule" preCondition="managedHandler" />
              </modules>
            </system.webServer>
            """);
        var app = WriteConfig("web.config", "<system.webServer><modules />" + "</system.webServer>");

        IisServerConfiguration.Load(baseline, app).Modules
            .Select(module => module.Name).ShouldBe(new[] { "Session" });
    }

    [Fact]
    public void A_Non_Boolean_Validation_Flag_Fails_Activation_Naming_The_File()
    {
        var app = WriteConfig(
            "web.config",
            """
            <system.web>
              <httpModules>
                <add name="LogA" type="Probe.LogA" />
              </httpModules>
            </system.web>
            <system.webServer>
              <validation validateIntegratedModeConfiguration="no" />
            </system.webServer>
            """);

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("validateIntegratedModeConfiguration");
        exception.Message.ShouldContain("web.config");
    }

    // Not covered by a reading: an element declaring no entries registers nothing, so the
    // narrower reading of MH23's "content of any kind" applies.
    [Fact]
    public void An_Empty_HttpModules_Element_Is_Not_Classic_Content()
    {
        var app = WriteConfig(
            "web.config", "<system.web><httpModules /><httpHandlers /></system.web>");

        IisServerConfiguration.Load(Baseline(), app).Modules
            .Select(module => module.Name).ShouldBe(new[] { "Session" });
    }
}
