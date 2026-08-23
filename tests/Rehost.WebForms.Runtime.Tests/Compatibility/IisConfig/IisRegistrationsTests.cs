using System.Configuration;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The measured system.webServer/modules and /handlers merge semantics (readings MH1-MH24), against
// real temp files because the loader's contract includes which file a refusal names.
public sealed class IisRegistrationsTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-iisreg-");

    public void Dispose() => _root.Delete(recursive: true);

    private string WriteConfig(string name, string systemWebServerContent)
    {
        var path = Path.Combine(_root.FullName, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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
          <add name="Session" type="Base.SessionStateModule" preCondition="managedHandler" />
          <add name="ScriptModule-4.0" type="Base.ScriptModule"
               preCondition="managedHandler,runtimeVersionv4.0" />
        </modules>
        <handlers>
          <add name="PageHandlerFactory-Integrated-4.0" path="*.aspx" verb="GET,HEAD,POST"
               type="Base.PageHandlerFactory" preCondition="integratedMode,runtimeVersionv4.0" />
          <add name="ExtensionlessUrlHandler-Integrated-4.0" path="*." verb="GET,HEAD,POST"
               type="Base.TransferRequestHandler" preCondition="integratedMode,runtimeVersionv4.0" />
          <add name="StaticFile" path="*" verb="*" modules="StaticFileModule" resourceType="Either"
               requireAccess="Read" />
        </handlers>
        """);

    private static string[] NamesOf(IReadOnlyList<IisRegistration> registrations) =>
        registrations.Select(registration => registration.Name).ToArray();

    private static readonly string[] BaselineModules =
        { "OutputCache", "Session", "ScriptModule-4.0" };

    private static readonly string[] BaselineHandlers =
    {
        "PageHandlerFactory-Integrated-4.0",
        "ExtensionlessUrlHandler-Integrated-4.0",
        "StaticFile",
    };

    [Fact]
    public void The_Baseline_Lists_Publish_In_File_Order()
    {
        var configuration = IisServerConfiguration.Load(
            Baseline(), Path.Combine(_root.FullName, "missing", "web.config"));

        NamesOf(configuration.Modules).ShouldBe(BaselineModules);
        NamesOf(configuration.Handlers).ShouldBe(BaselineHandlers);
    }

    // Reading MH1.
    [Fact]
    public void Fresh_Module_Adds_Land_At_The_Tail_In_Document_Order()
    {
        var app = WriteConfig(
            "web.config",
            """<modules><add name="LogA" type="Probe.LogA" /><add name="LogB" type="Probe.LogB" /></modules>""");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Modules).ShouldBe(
            new[] { "OutputCache", "Session", "ScriptModule-4.0", "LogA", "LogB" });
    }

    // Reading MH8: no specificity ranking, so a wildcard listed first beats a later exact path.
    [Fact]
    public void Fresh_Handler_Adds_Precede_The_Inherited_Rows_In_Document_Order()
    {
        var app = WriteConfig(
            "web.config",
            """
            <handlers>
              <add name="HB" path="*.aspx" verb="*" type="Probe.HandlerB" />
              <add name="HA" path="probe2.aspx" verb="*" type="Probe.HandlerA" />
            </handlers>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Handlers).ShouldBe(
            new[]
            {
                "HB",
                "HA",
                "PageHandlerFactory-Integrated-4.0",
                "ExtensionlessUrlHandler-Integrated-4.0",
                "StaticFile",
            });
    }

    // Reading MH2.
    [Fact]
    public void A_Re_Added_Module_Keeps_Its_Inherited_Position()
    {
        var app = WriteConfig(
            "web.config",
            """
            <modules>
              <remove name="Session" />
              <add name="Session" type="Probe.LogC" />
              <add name="LogA" type="Probe.LogA" />
            </modules>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Modules).ShouldBe(
            new[] { "OutputCache", "Session", "ScriptModule-4.0", "LogA" });
        configuration.Modules[1].Type.ShouldBe("Probe.LogC");
    }

    // Reading MH9v: the re-added name does not regain top priority over an add listed after it.
    [Fact]
    public void A_Re_Added_Handler_Keeps_Its_Inherited_Position_Behind_Fresh_Adds()
    {
        var app = WriteConfig(
            "web.config",
            """
            <handlers>
              <remove name="ExtensionlessUrlHandler-Integrated-4.0" />
              <add name="ExtensionlessUrlHandler-Integrated-4.0" path="*." verb="*"
                   type="Probe.HandlerB" preCondition="integratedMode,runtimeVersionv4.0" />
              <add name="ApiHandler" path="api" verb="*" type="Probe.HandlerA" />
            </handlers>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Handlers).ShouldBe(
            new[]
            {
                "ApiHandler",
                "PageHandlerFactory-Integrated-4.0",
                "ExtensionlessUrlHandler-Integrated-4.0",
                "StaticFile",
            });
        configuration.Handlers[2].Type.ShouldBe("Probe.HandlerB");
    }

    [Fact]
    public void A_Removed_Module_Leaves_The_List()
    {
        var app = WriteConfig("web.config", """<modules><remove name="Session" /></modules>""");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Modules).ShouldBe(new[] { "OutputCache", "ScriptModule-4.0" });
    }

    // Reading MH13.
    [Fact]
    public void A_Duplicate_Module_Add_Fails_Activation_Naming_The_File_And_Key()
    {
        var app = WriteConfig(
            "web.config",
            """<modules><add name="Dup" type="Probe.LogA" /><add name="Dup" type="Probe.LogB" /></modules>""");

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("Dup");
        exception.Message.ShouldContain("modules");
        exception.Message.ShouldContain("web.config");
    }

    // Reading MH19.
    [Fact]
    public void A_Duplicate_Handler_Add_Fails_Activation_Naming_The_File_And_Key()
    {
        var app = WriteConfig(
            "web.config",
            """
            <handlers>
              <add name="Dup" path="dup1.aspx" verb="*" type="Probe.HandlerA" />
              <add name="Dup" path="dup2.aspx" verb="*" type="Probe.HandlerB" />
            </handlers>
            """);

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("Dup");
        exception.Message.ShouldContain("handlers");
        exception.Message.ShouldContain("web.config");
    }

    // Not covered by a reading: readings C1-C3 for the sibling collections refuse the same way,
    // and IIS holds one collection across the inheritance, so a re-registration <remove>s first.
    [Fact]
    public void An_Add_Over_An_Inherited_Name_Without_A_Remove_Fails_Activation()
    {
        var app = WriteConfig(
            "web.config", """<modules><add name="Session" type="Probe.LogC" /></modules>""");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("Session");
    }

    // Reading MH14.
    [Fact]
    public void A_Remove_Of_An_Absent_Module_Is_Tolerated()
    {
        var app = WriteConfig(
            "web.config",
            """<modules><remove name="NoSuchModule" /><add name="LogA" type="Probe.LogA" /></modules>""");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Modules).ShouldBe(
            new[] { "OutputCache", "Session", "ScriptModule-4.0", "LogA" });
    }

    // Reading MH20.
    [Fact]
    public void A_Remove_Of_An_Absent_Handler_Is_Tolerated()
    {
        var app = WriteConfig(
            "web.config",
            """
            <handlers>
              <remove name="NoSuchHandler" />
              <add name="HA" path="probe2.aspx" verb="*" type="Probe.HandlerA" />
            </handlers>
            """);

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Handlers).ShouldBe(
            new[]
            {
                "HA",
                "PageHandlerFactory-Integrated-4.0",
                "ExtensionlessUrlHandler-Integrated-4.0",
                "StaticFile",
            });
    }

    // Reading MH15: the inherited entries are locked, so no cleared module pipeline is reachable.
    [Fact]
    public void Clear_In_Modules_Fails_Activation_As_A_Lock_Violation()
    {
        var app = WriteConfig(
            "web.config",
            """<modules><clear /><add name="LogA" type="Probe.LogA" /></modules>""");

        var exception = Should.Throw<ConfigurationErrorsException>(
            () => IisServerConfiguration.Load(Baseline(), app));

        exception.Message.ShouldContain("modules");
        exception.Message.ShouldContain("lock violation");
        exception.Message.ShouldContain("web.config");
    }

    // Reading MH18: legal here, unlike MH15's modules, and any unmatched URL then 404s.
    [Fact]
    public void Clear_In_Handlers_Empties_The_Inherited_Rows()
    {
        var app = WriteConfig(
            "web.config",
            """<handlers><clear /><add name="HA" path="probe2.aspx" verb="*" type="Probe.HandlerA" /></handlers>""");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Handlers).ShouldBe(new[] { "HA" });
        NamesOf(configuration.Modules).ShouldBe(BaselineModules);
    }

    // Reading MH24: inert even for requests inside that folder, so the merge never reads below
    // the application root.
    [Fact]
    public void A_Subfolder_Modules_Section_Is_Ignored()
    {
        WriteConfig(
            Path.Combine("sub", "web.config"),
            """<modules><add name="LogA" type="Probe.LogA" /></modules>""");
        var app = WriteConfig(
            "web.config", """<modules><add name="LogB" type="Probe.LogB" /></modules>""");

        var configuration = IisServerConfiguration.Load(Baseline(), app);

        NamesOf(configuration.Modules).ShouldBe(
            new[] { "OutputCache", "Session", "ScriptModule-4.0", "LogB" });
    }

    [Fact]
    public void A_Handler_Add_Without_A_Path_Fails_Activation_Naming_The_Attribute()
    {
        var app = WriteConfig(
            "web.config",
            """<handlers><add name="HA" verb="*" type="Probe.HandlerA" /></handlers>""");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("path");
    }

    [Fact]
    public void A_Module_Add_Without_A_Name_Fails_Activation_Naming_The_Attribute()
    {
        var app = WriteConfig("web.config", """<modules><add type="Probe.LogA" /></modules>""");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("name");
    }

    [Fact]
    public void An_Unsupported_Element_Fails_Activation_Naming_The_Section()
    {
        var app = WriteConfig("web.config", "<handlers><bogus /></handlers>");

        Should.Throw<ConfigurationErrorsException>(
                () => IisServerConfiguration.Load(Baseline(), app))
            .Message.ShouldContain("handlers");
    }
}
