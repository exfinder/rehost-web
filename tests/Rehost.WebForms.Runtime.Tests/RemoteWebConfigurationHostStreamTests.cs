using Shouldly;
using System.Web.Configuration;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

public sealed class RemoteWebConfigurationHostStreamTests
{
    [Fact]
    public void Construction_is_explicitly_unsupported()
    {
        var exception = Should.Throw<PlatformNotSupportedException>(() =>
            new RemoteWebConfigurationHostStream(false, "server", "web.config", null, null, null, null, null));

        exception.Message.ShouldBe("Remote web configuration is not supported by Rehost.WebForms.");
    }
}
