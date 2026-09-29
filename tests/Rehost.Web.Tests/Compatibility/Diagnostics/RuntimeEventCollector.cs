using System.Diagnostics.Tracing;

namespace Rehost.Web.Tests.Compatibility.Diagnostics;

internal sealed record CollectedEvent(int EventId, string EventName, IReadOnlyList<string> Payload);

internal sealed class RuntimeEventCollector : EventListener
{
    private readonly List<CollectedEvent> _events = [];

    internal IReadOnlyList<CollectedEvent> EventsWithId(int eventId)
    {
        lock (_events)
        {
            return _events.Where(e => e.EventId == eventId).ToArray();
        }
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "Rehost.Web")
        {
            EnableEvents(eventSource, EventLevel.Verbose);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventSource.Name != "Rehost.Web")
        {
            return;
        }

        lock (_events)
        {
            _events.Add(
                new CollectedEvent(
                    eventData.EventId,
                    eventData.EventName ?? "",
                    eventData.Payload?.Select(p => p as string ?? "").ToArray() ?? []));
        }
    }
}
