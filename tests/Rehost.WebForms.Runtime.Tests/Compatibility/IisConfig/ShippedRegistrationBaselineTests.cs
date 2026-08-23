using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.IisConfig;

// The shipped applicationHost baseline as an application receives it: the golden's integrated
// v4.0 registrations, transcribed in golden order.
public sealed class ShippedRegistrationBaselineTests
{
    private static IisServerConfiguration ShippedBaseline() =>
        IisServerConfiguration.Load(
            Path.Combine(
                AppContext.BaseDirectory, "configs", "rehost-webforms.applicationHost.config"),
            Path.Combine(AppContext.BaseDirectory, "no-such-application", "web.config"));

    // web.config answers 404 from this rule, not from the .config deny row that also covers it:
    // both refuse the same URL, so only the rule itself distinguishes them.
    [Fact]
    public void Web_Config_Is_A_Hidden_Segment()
    {
        ShippedBaseline().IsHiddenSegment("web.config").ShouldBeTrue();
        ShippedBaseline().IsHiddenSegment("WEB.CONFIG").ShouldBeTrue();
    }

    // MH1's Modules.AllKeys dump, which is the managed module list an application observes.
    [Fact]
    public void The_Module_List_Matches_The_Order_IIS_Reports()
    {
        var modules = ShippedBaseline().Modules;

        modules.Select(module => module.Name).ShouldBe(
            new[]
            {
                "OutputCache",
                "Session",
                "WindowsAuthentication",
                "FormsAuthentication",
                "DefaultAuthentication",
                "RoleManager",
                "UrlAuthorization",
                "FileAuthorization",
                "AnonymousIdentification",
                "Profile",
                "UrlMappingsModule",
                "UrlRoutingModule-4.0",
                "ScriptModule-4.0",
            });
        modules[1].Type.ShouldBe("System.Web.SessionState.SessionStateModule");
        modules[1].PreCondition.ShouldBe("managedHandler");
    }

    [Fact]
    public void The_Handler_List_Carries_The_Golden_Rows_This_Runtime_Can_Serve()
    {
        var handlers = ShippedBaseline().Handlers;

        handlers.Select(handler => handler.Name).ShouldBe(
            new[]
            {
                "TraceHandler-Integrated-4.0",
                "AssemblyResourceLoader-Integrated-4.0",
                "PageHandlerFactory-Integrated-4.0",
                "SimpleHandlerFactory-Integrated-4.0",
                "WebServiceHandlerFactory-Integrated-4.0",
                "aspq-Integrated-4.0",
                "cshtm-Integrated-4.0",
                "cshtml-Integrated-4.0",
                "vbhtm-Integrated-4.0",
                "vbhtml-Integrated-4.0",
                "ScriptHandlerFactoryAppServices-Integrated-4.0",
                "ScriptResourceIntegrated-4.0",
                "TRACEVerbHandler",
                "OPTIONSVerbHandler",
                "ExtensionlessUrlHandler-Integrated-4.0",
                "StaticFile",
            });
    }

    [Fact]
    public void The_Aspx_Row_Carries_Its_Golden_Mapping()
    {
        var page = ShippedBaseline().Handlers
            .Single(handler => handler.Name == "PageHandlerFactory-Integrated-4.0");

        page.Path.ShouldBe("*.aspx");
        page.Verb.ShouldBe("GET,HEAD,POST,DEBUG");
        page.Type.ShouldBe("System.Web.UI.PageHandlerFactory");
        page.PreCondition.ShouldBe("integratedMode,runtimeVersionv4.0");
    }

    // Reading MH10: every golden module row carries preCondition="managedHandler", so the shipped
    // defaults sit out the requests a native handler serves, while no handler row does.
    [Fact]
    public void Every_Baseline_Module_Row_Is_Marked_Per_Request_Conditional()
    {
        var configuration = ShippedBaseline();

        configuration.Modules.ShouldAllBe(module => module.RequiresManagedHandler);
        configuration.Handlers.ShouldAllBe(handler => !handler.RequiresManagedHandler);
    }

    // The native rows' modules= names stay unresolved text; the bridge to the port's
    // reimplementations is not this seam's business.
    [Fact]
    public void The_Native_Rows_Keep_Their_Module_Names_Unresolved()
    {
        var handlers = ShippedBaseline().Handlers;

        var staticFile = handlers[^1];
        staticFile.Name.ShouldBe("StaticFile");
        staticFile.Path.ShouldBe("*");
        staticFile.Verb.ShouldBe("*");
        staticFile.Type.ShouldBeNull();
        staticFile.Modules.ShouldBe(
            "StaticFileModule,DefaultDocumentModule,DirectoryListingModule");
        staticFile.ResourceType.ShouldBe("Either");
        staticFile.Attribute("requireAccess").ShouldBe("Read");

        handlers.Single(handler => handler.Name == "OPTIONSVerbHandler").Modules
            .ShouldBe("ProtocolSupportModule");
    }
}
