namespace System.Web.Util {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;

    internal enum AssemblyResolutionOutcome {
        Compiled,
        Loaded,
        Absent,
    }

    // Portable replacement for the native ASP.NET event-log reporter in webengine4.dll.
    // Callers keep their original sequence; only the platform operation differs.
    // Reporting must never fail the request being reported on, so the whole wrapper body is
    // swallowed — stringification and log providers can both throw.
    [EventSource(Name = WebFormsRuntimeLogger.Category)]
    internal sealed class WebFormsRuntimeEventSource : EventSource {
        internal static readonly WebFormsRuntimeEventSource Log = new WebFormsRuntimeEventSource();

        private WebFormsRuntimeEventSource() {
        }

        [NonEvent]
        private static void Swallow(Action report) {
            try {
                report();
            }
            catch {
            }
        }

        [NonEvent]
        private static string OutcomeText(AssemblyResolutionOutcome outcome) {
            switch (outcome) {
                case AssemblyResolutionOutcome.Compiled:
                    return "compiled";
                case AssemblyResolutionOutcome.Loaded:
                    return "loaded";
                default:
                    return "absent";
            }
        }

        [NonEvent]
        internal void UnhandledException(Exception exception, string eventInfo) {
            Swallow(() => {
                WebFormsRuntimeLogger.Logger.UnhandledException(exception, eventInfo);
                WriteUnhandledException(eventInfo);
            });
        }

        [Event(1, Level = EventLevel.Error, Message = "{0}")]
        private void WriteUnhandledException(string eventInfo) {
            WriteEvent(1, eventInfo);
        }

        // .NET Framework discards these exceptions with no record of any kind; the port reports
        // them so that a failed request is diagnosable. The response is unaffected either way.
        [NonEvent]
        internal void SwallowedException(string site, Exception swallowed, string context) {
            Swallow(() => {
                WebFormsRuntimeLogger.Logger.SwallowedException(swallowed, site, context);
                WriteSwallowedException(site, swallowed.ToString(), context);
            });
        }

        [Event(2, Level = EventLevel.Warning, Message = "{0} swallowed an exception; {2}: {1}")]
        private void WriteSwallowedException(string site, string exception, string context) {
            WriteEvent(2, site, exception, context);
        }

        [NonEvent]
        internal void AssemblyResolution(AssemblyResolutionOutcome outcome, string assemblyName) {
            Swallow(() => {
                var text = OutcomeText(outcome);
                WebFormsRuntimeLogger.Logger.AssemblyResolution(text, assemblyName);
                WriteAssemblyResolution(text, assemblyName);
            });
        }

        [Event(3, Level = EventLevel.Verbose, Message = "Assembly {1} {0}")]
        private void WriteAssemblyResolution(string outcome, string assemblyName) {
            WriteEvent(3, outcome, assemblyName);
        }

        [NonEvent]
        internal void CompilationFailed(string outputAssembly, IEnumerable<string> diagnostics) {
            Swallow(() => {
                var text = String.Join(Environment.NewLine, diagnostics);
                WebFormsRuntimeLogger.Logger.CompilationFailed(outputAssembly, text);
                WriteCompilationFailed(outputAssembly, text);
            });
        }

        [Event(4, Level = EventLevel.Error, Message = "Compilation of {0} failed: {1}")]
        private void WriteCompilationFailed(string outputAssembly, string diagnostics) {
            WriteEvent(4, outputAssembly, diagnostics);
        }

        [NonEvent]
        internal void MonitorSampleFailed(string monitor, Exception exception) {
            Swallow(() => {
                WebFormsRuntimeLogger.Logger.MonitorSampleFailed(exception, monitor);
                WriteMonitorSampleFailed(monitor, exception.ToString());
            });
        }

        [Event(5, Level = EventLevel.Warning, Message = "{0} sampling failed: {1}")]
        private void WriteMonitorSampleFailed(string monitor, string exception) {
            WriteEvent(5, monitor, exception);
        }

        [NonEvent]
        internal void MonitorDisabled(string monitor, Exception exception) {
            Swallow(() => {
                WebFormsRuntimeLogger.Logger.MonitorDisabled(exception, monitor);
                WriteMonitorDisabled(monitor, exception.ToString());
            });
        }

        [Event(6, Level = EventLevel.Error, Message = "{0} disabled after repeated failures: {1}")]
        private void WriteMonitorDisabled(string monitor, string exception) {
            WriteEvent(6, monitor, exception);
        }

        [NonEvent]
        internal void AutoGeneratedMachineKey(bool validationKey, bool decryptionKey, string keyFilePath) {
            Swallow(() => {
                var keys = validationKey && decryptionKey
                    ? "validationKey and decryptionKey"
                    : validationKey ? "validationKey" : "decryptionKey";
                WebFormsRuntimeLogger.Logger.AutoGeneratedMachineKey(keys, keyFilePath);
                WriteAutoGeneratedMachineKey(keys, keyFilePath);
            });
        }

        [Event(7, Level = EventLevel.Warning, Message =
            "machineKey {0} auto-generated; keys persist in '{1}' and survive process restart " +
            "on this machine. Scale-out across machines requires an explicit <machineKey> or " +
            "the REHOST_WEBFORMS_MACHINEKEY_* environment variables.")]
        private void WriteAutoGeneratedMachineKey(string keys, string keyFilePath) {
            WriteEvent(7, keys, keyFilePath);
        }

        [NonEvent]
        internal void RuntimeError(Exception exception, object source) {
            Swallow(() => {
                var sourceName = source == null ? "(none)" : source.GetType().FullName;
                WebFormsRuntimeLogger.Logger.RuntimeError(exception, sourceName);
                WriteRuntimeError(sourceName, exception.ToString());
            });
        }

        [Event(8, Level = EventLevel.Error, Message = "Unhandled runtime error from {0}: {1}")]
        private void WriteRuntimeError(string source, string exception) {
            WriteEvent(8, source, exception);
        }

        [NonEvent]
        internal void RestartRequested(ApplicationShutdownReason reason) {
            Swallow(() => {
                var text = reason.ToString();
                WebFormsRuntimeLogger.Logger.RestartRequested(text);
                WriteRestartRequested(text);
            });
        }

        [Event(9, Level = EventLevel.Warning, Message =
            "The runtime asked for the application to be rebuilt ({0}).")]
        private void WriteRestartRequested(string reason) {
            WriteEvent(9, reason);
        }

        [NonEvent]
        internal void SystemNetUnsupported(string file) {
            Swallow(() => {
                WebFormsRuntimeLogger.Logger.SystemNetUnsupported(file);
                WriteSystemNetUnsupported(file);
            });
        }

        [Event(10, Level = EventLevel.Error, Message =
            "<system.net> in {0} is not supported and declared for activation only; nothing on .NET reads it. Configure proxies, connection limits and mail settings in code.")]
        private void WriteSystemNetUnsupported(string file) {
            WriteEvent(10, file);
        }

        [NonEvent]
        internal void CachingProfilesIgnored(string file, string extensions) {
            Swallow(() => {
                WebFormsRuntimeLogger.Logger.CachingProfilesIgnored(file, extensions);
                WriteCachingProfilesIgnored(file, extensions);
            });
        }

        [Event(11, Level = EventLevel.Warning, Message =
            "<system.webServer><caching> in {0}: profiles for {1} are ignored; IIS served stored responses for them, this host runs every request.")]
        private void WriteCachingProfilesIgnored(string file, string extensions) {
            WriteEvent(11, file, extensions);
        }

        [NonEvent]
        internal void SectionNotHonored(string file, string element, string reason) {
            Swallow(() => {
                WebFormsRuntimeLogger.Logger.SectionNotHonored(file, element, reason);
                WriteSectionNotHonored(file, element, reason);
            });
        }

        [Event(12, Level = EventLevel.Warning, Message = "<{1}> in {0} is not honored; {2}")]
        private void WriteSectionNotHonored(string file, string element, string reason) {
            WriteEvent(12, file, element, reason);
        }

        [NonEvent]
        internal void CompilationOutput(string directory, string source) {
            Swallow(() => {
                WebFormsRuntimeLogger.Logger.CompilationOutput(directory, source);
                WriteCompilationOutput(directory, source);
            });
        }

        [Event(13, Level = EventLevel.Informational, Message = "Generated output goes to '{0}' ({1}).")]
        private void WriteCompilationOutput(string directory, string source) {
            WriteEvent(13, directory, source);
        }

        [NonEvent]
        internal void CompilationOutputInsideApplication(string directory, string source) {
            Swallow(() => {
                WebFormsRuntimeLogger.Logger.CompilationOutputInsideApplication(directory, source);
                WriteCompilationOutputInsideApplication(directory, source);
            });
        }

        [Event(14, Level = EventLevel.Warning, Message =
            "The compilation temporary directory '{0}' from the {1} is inside the application root; generated output there changes the application's own hash, so every restart recompiles. Move it outside the application.")]
        private void WriteCompilationOutputInsideApplication(string directory, string source) {
            WriteEvent(14, directory, source);
        }
    }
}
