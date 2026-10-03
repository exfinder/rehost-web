using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Shared;

public sealed class AssemblyRetargetsTests
{
    [Theory]
    [InlineData("System.Web", "Rehost.Web")]
    [InlineData("System.Web.Extensions", "Rehost.Web.Extensions")]
    [InlineData("System.Web.Services", "Rehost.Web.Services")]
    [InlineData("System.Web.ApplicationServices", "Rehost.Web.ApplicationServices")]
    [InlineData("System.Web.WebPages", "Rehost.Web.WebPages")]
    [InlineData("System.Web.WebPages.Razor", "Rehost.Web.WebPages.Razor")]
    [InlineData("System.Web.WebPages.Deployment", "Rehost.Web.WebPages.Deployment")]
    [InlineData("System.Web.Mvc", "Rehost.Web.Mvc")]
    [InlineData("System.Web.Http.WebHost", "Rehost.Web.Http.WebHost")]
    [InlineData("System.Web.Optimization", "Rehost.Web.Optimization")]
    [InlineData(
        "Microsoft.AspNet.Web.Optimization.WebForms", "Rehost.AspNet.Web.Optimization.WebForms")]
    [InlineData("Microsoft.Web.Infrastructure", "Rehost.Web.Infrastructure")]
    [InlineData("Microsoft.Owin.Host.SystemWeb", "Rehost.Owin.Host.SystemWeb")]
    public void A_Framework_Assembly_Name_Becomes_Its_Rehost_Assembly(
        string original, string rehost)
    {
        AssemblyRetargets.RetargetAssemblyName(original).ShouldBe(rehost);
    }

    [Fact]
    public void A_Display_Name_Loses_Version_Culture_Token_And_Architecture()
    {
        AssemblyRetargets.RetargetAssemblyName(
            "System.Web.WebPages.Razor, Version=3.0.0.0, Culture=neutral,"
            + " PublicKeyToken=31BF3856AD364E35, processorArchitecture=MSIL")
            .ShouldBe("Rehost.Web.WebPages.Razor");
    }

    [Fact]
    public void A_Qualified_Type_Name_Keeps_Its_Type_And_Loses_The_Identity_Tail()
    {
        AssemblyRetargets.Retarget(
            "System.Web.Mvc.MvcWebRazorHostFactory, System.Web.Mvc, Version=5.2.3.0,"
            + " Culture=neutral, PublicKeyToken=31BF3856AD364E35")
            .ShouldBe("System.Web.Mvc.MvcWebRazorHostFactory, Rehost.Web.Mvc");
    }

    [Fact]
    public void The_Assembly_Name_Match_Ignores_Case()
    {
        AssemblyRetargets.Retarget("Some.Module, SYSTEM.WEB, Version=4.0.0.0")
            .ShouldBe("Some.Module, Rehost.Web");
    }

    [Fact]
    public void A_Name_Sharing_Only_A_Prefix_With_A_Row_Is_Untouched()
    {
        AssemblyRetargets.Retarget("System.Web.Http.ApiController, System.Web.Http, Version=5.2.9.0")
            .ShouldBe("System.Web.Http.ApiController, System.Web.Http, Version=5.2.9.0");
    }

    [Fact]
    public void An_Application_Assembly_Is_Untouched()
    {
        AssemblyRetargets.Retarget("Contoso.Auth.TenantModule, Contoso.Auth, Version=2.1.0.0")
            .ShouldBe("Contoso.Auth.TenantModule, Contoso.Auth, Version=2.1.0.0");
        AssemblyRetargets.RetargetAssemblyName("*").ShouldBe("*");
    }

    [Fact]
    public void An_Unqualified_Type_Name_Is_Untouched()
    {
        AssemblyRetargets.Retarget("System.Web.Mvc.WebViewPage")
            .ShouldBe("System.Web.Mvc.WebViewPage");
    }

    [Fact]
    public void A_Generic_Arguments_Identity_Is_Untouched_When_The_Outer_One_Matches()
    {
        AssemblyRetargets.Retarget(
            "Contoso.Wrapper`1[[System.Web.HttpContext, System.Web, Version=4.0.0.0]],"
            + " System.Web.Extensions, Version=4.0.0.0")
            .ShouldBe(
                "Contoso.Wrapper`1[[System.Web.HttpContext, System.Web, Version=4.0.0.0]],"
                + " Rehost.Web.Extensions");
    }
}
