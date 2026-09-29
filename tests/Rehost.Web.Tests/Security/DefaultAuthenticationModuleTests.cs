using System.Web.Security;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Security;

public sealed class DefaultAuthenticationModuleTests
{
    private static void Handler(object sender, DefaultAuthenticationEventArgs e)
    {
    }

    [Fact]
    public void Subscribing_Refuses_Naming_The_Event()
    {
        var module = new DefaultAuthenticationModule();

        var failure = Should.Throw<PlatformNotSupportedException>(
            () => module.Authenticate += Handler);

        failure.Message.ShouldContain("DefaultAuthentication.Authenticate", Case.Sensitive);
    }

    [Fact]
    public void Unsubscribing_Is_Still_Accepted()
    {
        var module = new DefaultAuthenticationModule();

        Should.NotThrow(() => module.Authenticate -= Handler);
    }
}
