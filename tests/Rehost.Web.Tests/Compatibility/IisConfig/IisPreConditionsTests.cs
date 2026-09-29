using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.IisConfig;

// The measured preCondition semantics (readings MH10-MH12, MH17, MH25) at the snapshot seam: the
// pool is permanently integrated, v4.0 and 64-bit, and managedHandler survives evaluation as a
// per-request flag.
public sealed class IisPreConditionsTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-iispre-");

    public void Dispose() => _root.Delete(recursive: true);

    private string WriteConfig(string name, string systemWebServerContent)
    {
        var path = Path.Combine(_root.FullName, name);
        File.WriteAllText(
            path,
            """<?xml version="1.0"?><configuration><system.webServer>"""
            + systemWebServerContent
            + "</system.webServer></configuration>");
        return path;
    }

    private string Baseline() => WriteConfig(
        "baseline.config",
        """
        <modules>
          <add name="OutputCache" type="Base.OutputCacheModule" preCondition="managedHandler" />
          <add name="UrlRoutingModule-4.0" type="Base.UrlRoutingModule"
               preCondition="managedHandler,runtimeVersionv4.0" />
        </modules>
        <handlers>
          <add name="PageHandlerFactory-Integrated-4.0" path="*.aspx" verb="GET,HEAD,POST"
               type="Base.PageHandlerFactory" preCondition="integratedMode,runtimeVersionv4.0" />
          <add name="StaticFile" path="*" verb="*" modules="StaticFileModule" />
        </handlers>
        """);

    private static string[] NamesOf(IReadOnlyList<IisRegistration> registrations) =>
        registrations.Select(registration => registration.Name).ToArray();

    private static readonly string[] BaselineModules = { "OutputCache", "UrlRoutingModule-4.0" };

    private static readonly string[] BaselineHandlers =
        { "PageHandlerFactory-Integrated-4.0", "StaticFile" };

    // Reading MH11: absent from Modules.AllKeys, with no error.
    [Fact]
    public void A_ClassicMode_Module_Is_Absent_From_The_Published_List()
    {
        var app = WriteConfig(
            "web.config",
            """
            <modules>
              <add name="LogA" type="Probe.LogA" preCondition="classicMode" />
              <add name="LogB" type="Probe.LogB" preCondition="integratedMode" />
            </modules>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Modules).ShouldBe(
            new[] { "OutputCache", "UrlRoutingModule-4.0", "LogB" });
    }

    // Reading MH25a: the unsatisfied entry leaves the walk, so the next mapping matches.
    [Fact]
    public void A_ClassicMode_Handler_Is_Absent_From_The_Published_List()
    {
        var app = WriteConfig(
            "web.config",
            """
            <handlers>
              <add name="PC" path="probe2.aspx" verb="*" type="Probe.HandlerA"
                   preCondition="classicMode" />
              <add name="W" path="*.aspx" verb="*" type="Probe.HandlerB"
                   preCondition="integratedMode,runtimeVersionv4.0" />
            </handlers>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Handlers).ShouldBe(
            new[] { "W", "PageHandlerFactory-Integrated-4.0", "StaticFile" });
    }

    // Reading MH10: LogA skipped the static request, LogB ran on it.
    [Fact]
    public void A_ManagedHandler_Entry_Is_Published_Marked_Per_Request_Conditional()
    {
        var app = WriteConfig(
            "web.config",
            """
            <modules>
              <add name="LogA" type="Probe.LogA" preCondition="managedHandler" />
              <add name="LogB" type="Probe.LogB" />
            </modules>
            """);

        var modules = IisServerConfiguration.Load(Baseline(), app).Modules;

        NamesOf(modules).ShouldBe(
            new[] { "OutputCache", "UrlRoutingModule-4.0", "LogA", "LogB" });
        modules.Select(module => module.RequiresManagedHandler)
            .ShouldBe(new[] { true, true, true, false });
    }

    // Reading MH17: the same pair as MH10 both ran on the static request under the toggle.
    [Fact]
    public void Run_All_Managed_Modules_Nullifies_The_ManagedHandler_Condition()
    {
        var app = WriteConfig(
            "web.config",
            """
            <modules runAllManagedModulesForAllRequests="true">
              <add name="LogA" type="Probe.LogA" preCondition="managedHandler" />
              <add name="LogB" type="Probe.LogB" />
            </modules>
            """);

        var modules = IisServerConfiguration.Load(Baseline(), app).Modules;

        NamesOf(modules).ShouldBe(
            new[] { "OutputCache", "UrlRoutingModule-4.0", "LogA", "LogB" });
        modules.Select(module => module.RequiresManagedHandler)
            .ShouldBe(new[] { false, false, false, false });
        modules[0].PreCondition.ShouldBe("managedHandler");
    }

    [Fact]
    public void Run_All_Managed_Modules_False_Keeps_The_ManagedHandler_Condition()
    {
        var app = WriteConfig(
            "web.config",
            """
            <modules runAllManagedModulesForAllRequests="false">
              <add name="LogA" type="Probe.LogA" preCondition="managedHandler" />
            </modules>
            """);

        var modules = IisServerConfiguration.Load(Baseline(), app).Modules;

        modules.Single(module => module.Name == "LogA").RequiresManagedHandler.ShouldBeTrue();
    }

    [Fact]
    public void A_Non_Boolean_Run_All_Managed_Modules_Fails_Activation_Naming_The_File()
    {
        var app = WriteConfig(
            "web.config",
            """<modules runAllManagedModulesForAllRequests="yes"><add name="LogA" type="Probe.LogA" /></modules>""");

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("runAllManagedModulesForAllRequests", Case.Sensitive);
        exception.Message.ShouldContain("web.config");
    }

    // Reading MH12: 500.0 "Module "LogA" has a bad precondition "bogus"" on every request.
    [Fact]
    public void An_Unknown_Module_PreCondition_Fails_Activation_Naming_The_Entry_And_Token()
    {
        var app = WriteConfig(
            "web.config",
            """<modules><add name="LogA" type="Probe.LogA" preCondition="bogus" /></modules>""");

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("Module", Case.Sensitive);
        exception.Message.ShouldContain("LogA", Case.Sensitive);
        exception.Message.ShouldContain("bogus");
        exception.Message.ShouldContain("web.config");
    }

    // Reading MH25b: the same shape with "Handler" in the message.
    [Fact]
    public void An_Unknown_Handler_PreCondition_Fails_Activation_Naming_The_Entry_And_Token()
    {
        var app = WriteConfig(
            "web.config",
            """
            <handlers>
              <add name="PC" path="probe2.aspx" verb="*" type="Probe.HandlerA"
                   preCondition="integratedMode,bogus" />
            </handlers>
            """);

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("Handler", Case.Sensitive);
        exception.Message.ShouldContain("PC", Case.Sensitive);
        exception.Message.ShouldContain("bogus");
        exception.Message.ShouldContain("web.config");
    }

    // Golden vocabulary the port answers from its fixed pool identity: 64-bit, so bitness32 is
    // unsatisfied rather than unknown (no reading; refusing it would reject valid IIS config).
    [Fact]
    public void Bitness64_Is_Satisfied_And_Bitness32_Drops_The_Entry()
    {
        var app = WriteConfig(
            "web.config",
            """
            <modules>
              <add name="Wide" type="Probe.LogA" preCondition="bitness64" />
              <add name="Narrow" type="Probe.LogB" preCondition="bitness32" />
            </modules>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Modules).ShouldBe(
            new[] { "OutputCache", "UrlRoutingModule-4.0", "Wide" });
    }

    // Same fixed identity, v4.0: the golden's v1.1/v2.0 rows are inapplicable, not unknown.
    [Fact]
    public void Legacy_Runtime_Version_Tokens_Drop_The_Entry()
    {
        var app = WriteConfig(
            "web.config",
            """
            <modules>
              <add name="Legacy11" type="Probe.LogA" preCondition="runtimeVersionv1.1" />
              <add name="Legacy20" type="Probe.LogB"
                   preCondition="managedHandler,runtimeVersionv2.0" />
              <add name="Current" type="Probe.LogC" preCondition="managedHandler,runtimeVersionv4.0" />
            </modules>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Modules).ShouldBe(
            new[] { "OutputCache", "UrlRoutingModule-4.0", "Current" });
    }

    [Fact]
    public void An_Unsatisfied_Token_Beside_ManagedHandler_Still_Drops_The_Entry()
    {
        var app = WriteConfig(
            "web.config",
            """<modules><add name="LogA" type="Probe.LogA" preCondition="managedHandler,classicMode" /></modules>""");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Modules).ShouldBe(BaselineModules);
    }

    [Fact]
    public void An_Empty_PreCondition_Publishes_The_Entry_Unconditionally()
    {
        var app = WriteConfig(
            "web.config",
            """
            <handlers>
              <add name="HA" path="probe2.aspx" verb="*" type="Probe.HandlerA" preCondition="" />
            </handlers>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Handlers).ShouldBe(new[] { "HA" }.Concat(BaselineHandlers).ToArray());
        configuration.Handlers[0].RequiresManagedHandler.ShouldBeFalse();
    }

    // A dropped entry still owns its name for the merge: IIS evaluates preconditions after the
    // collection is built, so the duplicate is the authoring error it always was.
    [Fact]
    public void A_Dropped_Entry_Still_Occupies_Its_Name()
    {
        var app = WriteConfig(
            "web.config",
            """
            <modules>
              <add name="LogA" type="Probe.LogA" preCondition="classicMode" />
              <add name="LogA" type="Probe.LogB" />
            </modules>
            """);

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("LogA", Case.Sensitive);
    }
}
