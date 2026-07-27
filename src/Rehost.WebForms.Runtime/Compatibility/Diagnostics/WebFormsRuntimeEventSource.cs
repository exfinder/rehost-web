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

        // .NET Framework discards these exceptions with no record of any kind; the port reports
        // them so that a failed request is diagnosable. The response is unaffected either way.
        [Event(2, Level = EventLevel.Error, Message = "{0}: {1}")]
        internal void SwallowedRequestException(string site, string exception) {
            WriteEvent(2, site, exception);
        }

        [Event(3, Level = EventLevel.Informational, Message = "{0}: {1}")]
        internal void BinAssemblyResolution(string outcome, string assemblyName) {
            WriteEvent(3, outcome, assemblyName);
        }
    }
}
