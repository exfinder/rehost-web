using System.Configuration.Internal;
using System.Web.Configuration;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Configuration;

// The configuration system marks a stream monitored before it asks the host about notifications
// and stops on that mark alone, so a host answering false receives Stop for streams never started.
public sealed class WebConfigurationHostTests
{
    [Fact]
    public void Stop_Without_Start_Is_A_No_Op()
    {
        var host = new WebConfigurationHost();

        host.SupportsChangeNotifications.ShouldBeFalse();
        Should.NotThrow(() => host.StopMonitoringStreamForChanges(
            "/never/started/web.config", (StreamChangeCallback)(_ => { })));
    }
}
