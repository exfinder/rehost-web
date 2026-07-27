namespace System.Web.Util {
    using System.Diagnostics.Tracing;

    // Portable replacement for the native ASP.NET event-log reporter in webengine4.dll.
    // Callers keep their original sequence; only the platform operation differs.
    [EventSource(Name = "Rehost.WebForms.Runtime")]
    internal sealed class WebFormsRuntimeEventSource : EventSource {
        internal static readonly WebFormsRuntimeEventSource Log = new WebFormsRuntimeEventSource();

        private WebFormsRuntimeEventSource() {
        }

        [Event(1, Level = EventLevel.Error, Message = "{0}")]
        internal void UnhandledException(string eventInfo) {
            WriteEvent(1, eventInfo);
        }
    }
}
