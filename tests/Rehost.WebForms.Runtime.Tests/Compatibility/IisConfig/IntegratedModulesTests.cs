using System.Configuration;
using System.Web;
using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The supplier that turns the published snapshot into the list HttpApplication's integrated-mode
// builder consumes, against real temp files because a refusal's wording names the entry.
public sealed class IntegratedModulesTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("rehost-intmod-");

    public void Dispose() => _root.Delete(recursive: true);

    private IReadOnlyList<ModuleConfigurationInfo> ConfigInfo(string modulesContent)
    {
        var path = Path.Combine(_root.FullName, "baseline.config");
        File.WriteAllText(
            path,
            """<?xml version="1.0"?><configuration><system.webServer><modules>"""
            + modulesContent
            + "</modules></system.webServer></configuration>");

        var configuration = IisServerConfiguration.Load(
            path, Path.Combine(_root.FullName, "missing", "web.config"));

        return IntegratedModules.ConfigInfo(configuration.Modules, new ManagedHandlerModules());
    }

    [Fact]
    public void An_Unqualified_Golden_Type_Resolves_Against_The_Runtime_Assembly()
    {
        var modules = ConfigInfo(
            """<add name="OutputCache" type="System.Web.Caching.OutputCacheModule" preCondition="managedHandler" />""");

        modules.Count.ShouldBe(1);
        modules[0].Name.ShouldBe("OutputCache");
        modules[0].Type.ShouldBe("System.Web.Caching.OutputCacheModule");
    }

    [Fact]
    public void A_Framework_Extensions_Type_Resolves_Through_The_Ports_Extensions_Assembly()
    {
        var modules = ConfigInfo(
            """
            <add name="ScriptModule-4.0"
                 type="System.Web.Handlers.ScriptModule, System.Web.Extensions, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"
                 preCondition="managedHandler" />
            """);

        modules[0].Type.ShouldBe("System.Web.Handlers.ScriptModule, Rehost.WebForms.Extensions");
    }

    // Reading MH10: the condition survives evaluation as the per-request question the pipeline
    // answers, and rides to the module info the collection is built from.
    [Fact]
    public void The_ManagedHandler_Condition_Rides_Through_And_An_Unconditioned_Row_Carries_None()
    {
        var modules = ConfigInfo(
            """
            <add name="OutputCache" type="System.Web.Caching.OutputCacheModule"
                 preCondition="managedHandler" />
            <add name="UrlMappingsModule" type="System.Web.UrlMappingsModule" />
            """);

        modules[0].Precondition.ShouldBe("managedHandler");
        modules[1].Precondition.ShouldBe(string.Empty);
    }

    // Reading MH22b: a module type that cannot load kills every request to the application, so the
    // diagnostic has to name the entry that caused it.
    [Fact]
    public void A_Type_That_Cannot_Load_Refuses_Naming_The_Entry()
    {
        var refusal = Should.Throw<ConfigurationErrorsException>(
            () => ConfigInfo("""<add name="BadM" type="No.Such.Type, NoAsm" />"""));

        refusal.Message.ShouldContain("name=\"BadM\"");
        refusal.Message.ShouldContain("No.Such.Type, NoAsm");
        refusal.Message.ShouldContain("static files");
    }

    [Fact]
    public void A_Type_That_Is_Not_A_Module_Refuses_Naming_The_Entry()
    {
        var refusal = Should.Throw<ConfigurationErrorsException>(
            () => ConfigInfo("""<add name="NotAModule" type="System.Web.HttpContext" />"""));

        refusal.Message.ShouldContain("name=\"NotAModule\"");
        refusal.Message.ShouldContain("IHttpModule");
    }

    [Fact]
    public void A_Row_Without_A_Type_Refuses_Naming_The_Entry()
    {
        var refusal = Should.Throw<ConfigurationErrorsException>(
            () => ConfigInfo("""<add name="Typeless" />"""));

        refusal.Message.ShouldContain("name=\"Typeless\"");
        refusal.Message.ShouldContain("no type=");
    }
}
