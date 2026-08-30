using System.Web;
using System.Web.Util;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Rehost.WebForms.Runtime.Tests.Compatibility.Diagnostics;

[Collection(nameof(ApplicationBootstrapCollection))]
public sealed class WebFormsRuntimeLoggerChannelTests : IDisposable
{
    public void Dispose()
    {
        WebFormsRuntimeLogger.ResetForTests();
    }

    [Fact]
    public void Swallowed_Request_Exception_Reaches_Both_Channels()
    {
        var factory = new CollectingLoggerFactory();
        WebFormsRuntimeLogger.Publish(factory);
        using var listener = new RuntimeEventCollector();
        var swallowed = new InvalidOperationException("reporting failed");

        WebFormsRuntimeEventSource.Log.SwallowedRequestException(
            "HttpRuntime.FinishRequest",
            swallowed,
            "request: original boom");

        var entry = factory.Entries.ShouldHaveSingleItem();
        entry.Category.ShouldBe("Rehost.WebForms.Runtime");
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Exception.ShouldBeSameAs(swallowed);
        entry.Message.ShouldContain("HttpRuntime.FinishRequest");
        entry.Message.ShouldContain("request: original boom");
        entry.State.ShouldContain(new KeyValuePair<string, object?>("site", "HttpRuntime.FinishRequest"));

        var written = listener.EventsWithId(2).ShouldHaveSingleItem();
        written.Payload[0].ShouldBe("HttpRuntime.FinishRequest");
        written.Payload[1].ShouldContain("reporting failed");
        written.Payload[2].ShouldBe("request: original boom");
    }

    [Fact]
    public void Without_A_Published_Factory_Only_The_Event_Source_Sees_The_Report()
    {
        var unpublished = new CollectingLoggerFactory();
        using var listener = new RuntimeEventCollector();

        WebFormsRuntimeEventSource.Log.MonitorSampleFailed(
            "LowPhysicalMemoryMonitor",
            new IOException("sampling failed"));

        unpublished.Entries.ShouldBeEmpty();
        listener.EventsWithId(5).ShouldHaveSingleItem()
            .Payload[1].ShouldContain("sampling failed");
    }

    [Fact]
    public void Unhandled_Exception_Keeps_Framework_Message_Content_On_The_Event_Source()
    {
        var factory = new CollectingLoggerFactory();
        WebFormsRuntimeLogger.Publish(factory);
        using var listener = new RuntimeEventCollector();
        var exception = Thrown(new HttpException("page blew up"));

        Misc.ReportUnhandledException(exception, ["Unhandled execution error", " | app: ", "test-app"]);

        var written = listener.EventsWithId(1).ShouldHaveSingleItem();
        written.Payload[0].ShouldStartWith("Unhandled execution error | app: test-app");
        written.Payload[0].ShouldContain("System.Web.HttpException");
        written.Payload[0].ShouldContain("page blew up");
        written.Payload[0].ShouldContain(nameof(Thrown));
        factory.Entries.ShouldHaveSingleItem().Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public void A_Hostile_ToString_Does_Not_Escape_The_Wrapper()
    {
        var factory = new CollectingLoggerFactory();
        WebFormsRuntimeLogger.Publish(factory);
        using var listener = new RuntimeEventCollector();

        WebFormsRuntimeEventSource.Log.MonitorDisabled("RecycleLimitMonitor", new HostileException());

        factory.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Error);
        listener.EventsWithId(6).ShouldBeEmpty();
    }

    [Fact]
    public void A_Failing_Log_Provider_Does_Not_Escape_The_Wrapper()
    {
        var factory = new CollectingLoggerFactory(() => new InvalidOperationException("provider is broken"));
        WebFormsRuntimeLogger.Publish(factory);

        WebFormsRuntimeEventSource.Log.MonitorDisabled("RecycleLimitMonitor", new IOException("sampling failed"));

        factory.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void A_Disposed_Factory_Neither_Throws_Nor_Wedges_The_Channel()
    {
        var disposed = new CollectingLoggerFactory();
        WebFormsRuntimeLogger.Publish(disposed);
        disposed.Dispose();

        WebFormsRuntimeEventSource.Log.MonitorSampleFailed("RecycleLimitMonitor", new IOException("late failure"));

        var replacement = new CollectingLoggerFactory();
        WebFormsRuntimeLogger.Publish(replacement);
        WebFormsRuntimeEventSource.Log.MonitorSampleFailed("RecycleLimitMonitor", new IOException("later failure"));
        replacement.Entries.ShouldHaveSingleItem().Message.ShouldContain("RecycleLimitMonitor");
        disposed.Entries.ShouldBeEmpty();
    }

    private static Exception Thrown(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception caught)
        {
            return caught;
        }
    }

    private sealed class HostileException : Exception
    {
        public override string ToString() => throw new NotSupportedException("no text for you");
    }
}
