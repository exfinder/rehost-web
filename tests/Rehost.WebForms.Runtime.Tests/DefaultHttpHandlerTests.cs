using System.Web;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests;

// The port's static-file serving moved to StaticFileBridgeHandler, which is what lets this
// entry point refuse; restoring static handling here would undo that split (ledger P93).
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
