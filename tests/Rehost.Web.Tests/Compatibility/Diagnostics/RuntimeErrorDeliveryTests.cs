using System.Web.Management;
using System.Web.Util;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Rehost.Web.Tests.Compatibility.Diagnostics;

[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class RuntimeErrorDeliveryTests : IDisposable
{
    public void Dispose()
    {
        RehostWebLogger.ResetForTests();
    }

    [Fact]
    public void A_Request_Error_Reaches_Both_Channels_Without_Health_Monitoring()
    {
        UnconfiguredHealthMonitoring();
        var factory = new CollectingLoggerFactory();
        RehostWebLogger.Publish(factory);
        using var listener = new RuntimeEventCollector();
        var error = new IOException("request failed");

        WebBaseEvent.RaiseRuntimeError(error, this);

        var entry = factory.Entries.ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(8);
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Exception.ShouldBeSameAs(error);
        var written = listener.EventsWithId(8).ShouldHaveSingleItem();
        written.Payload[0].ShouldBe(typeof(RuntimeErrorDeliveryTests).FullName);
        written.Payload[1].ShouldContain("request failed");
    }

    [Fact]
    public void A_Hostile_Request_Error_Does_Not_Escape_RaiseRuntimeError()
    {
        UnconfiguredHealthMonitoring();
        var factory = new CollectingLoggerFactory();
        RehostWebLogger.Publish(factory);

        WebBaseEvent.RaiseRuntimeError(new HostileException(), this);

        factory.Entries.ShouldHaveSingleItem().EventId.Id.ShouldBe(8);
    }

    // The first read of the section throws in an unactivated process and caches a null manager;
    // every later read answers false, which is the state these tests act on.
    private static void UnconfiguredHealthMonitoring()
    {
        try
        {
            _ = HealthMonitoringManager.Enabled;
        }
        catch (Exception)
        {
        }

        HealthMonitoringManager.Enabled.ShouldBeFalse();
    }

    private sealed class HostileException : Exception
    {
        public override string ToString() => throw new NotSupportedException("no text for you");
    }
}
