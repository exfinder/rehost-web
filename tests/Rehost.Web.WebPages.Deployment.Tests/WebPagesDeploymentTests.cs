using System.Web.WebPages.Deployment;
using Shouldly;
using Xunit;

namespace Rehost.Web.WebPages.Deployment.Tests;

public sealed class WebPagesDeploymentTests
{
    [Fact]
    public void Get_Incompatible_Dependencies_Refuses_Naming_The_Second_AppDomain()
    {
        var failure = Should.Throw<PlatformNotSupportedException>(
            () => WebPagesDeployment.GetIncompatibleDependencies(Path.GetTempPath()));

        failure.Message.ShouldBe(
            "WebPagesDeployment.GetIncompatibleDependencies loads the application's bin assemblies into a second AppDomain, which .NET does not support.");
    }

    [Fact]
    public void Get_Assembly_Path_Refuses_Naming_The_Registry_Key()
    {
        var failure = Should.Throw<PlatformNotSupportedException>(
            () => WebPagesDeployment.GetAssemblyPath(new Version(3, 0)));

        failure.Message.ShouldBe(
            @"WebPagesDeployment.GetAssemblyPath reads the install path from the registry key 'HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\ASP.NET Web Pages\v3.0', which only a Web Pages installer on Windows writes.");
    }
}
