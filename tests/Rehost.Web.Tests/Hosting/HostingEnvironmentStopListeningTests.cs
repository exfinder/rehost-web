using System.Web.Hosting;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Hosting;

// The port-owned entry point behind IV30's early stop signal. IIS reached OnGlobalStopListening
// through a native wait handle the port never sets up, so the host's stopping notification calls
// this instead. The registered-object half runs only inside a hosting environment and is covered
// by StopListeningOverKestrelTests.
public sealed class HostingEnvironmentStopListeningTests
{
    [Fact]
    public void Raising_It_Delivers_The_Public_Event_And_Latches_The_Flag()
    {
        var raised = 0;
        void Handler(object? sender, EventArgs e) => raised++;

        HostingEnvironment.StopListening += Handler;
        try
        {
            HostingEnvironment.RaiseStopListening();
        }
        finally
        {
            HostingEnvironment.StopListening -= Handler;
        }

        raised.ShouldBe(1);
        HostingEnvironment.StopListeningWasCalled.ShouldBeTrue();
    }
}
