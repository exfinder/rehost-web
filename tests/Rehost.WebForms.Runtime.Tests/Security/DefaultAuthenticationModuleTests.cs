using System.Web.Security;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Security;

// The classic DefaultAuthentication step this event fires from does exist here, so the refusal
// is a deliberate match of integrated's, not a missing implementation (ledger P93).
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
