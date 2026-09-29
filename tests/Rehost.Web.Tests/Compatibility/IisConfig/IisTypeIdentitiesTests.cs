using System.Web.IisConfig;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.IisConfig;

// The baseline rows are transcribed from the IIS golden, so they name Framework's assemblies.
public sealed class IisTypeIdentitiesTests
{
    [Fact]
    public void An_Unqualified_Name_Is_Left_For_The_Type_Resolver()
    {
        IisTypeIdentities.Retarget("System.Web.Caching.OutputCacheModule")
            .ShouldBe("System.Web.Caching.OutputCacheModule");
    }

    [Fact]
    public void The_System_Web_Identity_Becomes_The_Runtime_Assembly()
    {
        IisTypeIdentities.Retarget(
            "System.Web.Security.FormsAuthenticationModule, System.Web, Version=4.0.0.0,"
            + " Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
            .ShouldBe("System.Web.Security.FormsAuthenticationModule, Rehost.Web");
    }

    [Fact]
    public void The_System_Web_Extensions_Identity_Becomes_The_Extensions_Assembly()
    {
        IisTypeIdentities.Retarget(
            "System.Web.Handlers.ScriptModule, System.Web.Extensions, Version=4.0.0.0,"
            + " Culture=neutral, PublicKeyToken=31BF3856AD364E35")
            .ShouldBe("System.Web.Handlers.ScriptModule, Rehost.Web.Extensions");
    }

    [Fact]
    public void The_System_Web_Services_Identity_Becomes_The_Web_Services_Assembly()
    {
        IisTypeIdentities.Retarget(
            "System.Web.Services.Protocols.WebServiceHandlerFactory, System.Web.Services,"
            + " Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
            .ShouldBe(
                "System.Web.Services.Protocols.WebServiceHandlerFactory,"
                + " Rehost.Web.Services");
    }

    [Fact]
    public void An_Application_Assembly_Is_Untouched()
    {
        IisTypeIdentities.Retarget("Contoso.Auth.TenantModule, Contoso.Auth, Version=2.1.0.0")
            .ShouldBe("Contoso.Auth.TenantModule, Contoso.Auth, Version=2.1.0.0");
    }

    // Assembly names compare case-insensitively everywhere else in the loader.
    [Fact]
    public void The_Assembly_Name_Match_Ignores_Case()
    {
        IisTypeIdentities.Retarget("Some.Module, SYSTEM.WEB, Version=4.0.0.0")
            .ShouldBe("Some.Module, Rehost.Web");
    }

    // The first top-level comma ends the type name; the commas inside a generic argument list
    // belong to the arguments' own identities.
    [Fact]
    public void A_Generic_Arguments_Identity_Is_Not_Mistaken_For_The_Outer_One()
    {
        IisTypeIdentities.Retarget(
            "Contoso.Wrapper`1[[System.Web.HttpContext, System.Web, Version=4.0.0.0]],"
            + " System.Web.Extensions, Version=4.0.0.0")
            .ShouldBe(
                "Contoso.Wrapper`1[[System.Web.HttpContext, System.Web, Version=4.0.0.0]],"
                + " Rehost.Web.Extensions");
    }
}
