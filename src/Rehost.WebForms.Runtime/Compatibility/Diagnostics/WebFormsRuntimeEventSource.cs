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
        internal void AssemblyResolution(string outcome, string assemblyName) {
            WriteEvent(3, outcome, assemblyName);
        }

        [Event(4, Level = EventLevel.Error, Message = "{0}: {1}")]
        internal void CompilationFailed(string outputAssembly, string diagnostics) {
            WriteEvent(4, outputAssembly, diagnostics);
        }

        [Event(5, Level = EventLevel.Warning, Message = "{0} sampling failed: {1}")]
        internal void MonitorSampleFailed(string monitor, string exception) {
            WriteEvent(5, monitor, exception);
        }

        [Event(6, Level = EventLevel.Error, Message = "{0} disabled after repeated failures: {1}")]
        internal void MonitorDisabled(string monitor, string exception) {
            WriteEvent(6, monitor, exception);
        }
    }
}
