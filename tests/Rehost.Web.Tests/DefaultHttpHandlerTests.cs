using System.Web;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests;

public sealed class DefaultHttpHandlerTests
{
    [Fact]
    public void BeginProcessRequest_Refuses_Naming_The_Method()
    {
        var failure = Should.Throw<PlatformNotSupportedException>(
            () => new DefaultHttpHandler().BeginProcessRequest(null, null, null));

        failure.Message.ShouldContain(
            "DefaultHttpHandler.BeginProcessRequest", Case.Sensitive);
    }
}
