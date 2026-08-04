using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace Rehost.WebForms.Parity.Harness;

public sealed class RuntimeDiagnosticListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "Rehost.WebForms.Runtime")
        {
            EnableEvents(eventSource, EventLevel.Error);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventSource.Name != "Rehost.WebForms.Runtime")
        {
            return;
        }

        Console.Error.WriteLine(
            "runtime/"
            + eventData.EventName
            + ": "
            + string.Join(
                " | ",
                eventData.Payload ?? (IEnumerable<object?>)Array.Empty<object?>()));
    }
}
