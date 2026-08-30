# Portable diagnostics boundary

Framework runtime diagnostics end in Windows-only sinks: unhandled and
swallowed errors go through `webengine4.dll` to the Windows Event Log, health
monitoring delivers request errors and failure audits to the Event Log by
default, and request tracing is IIS ETW. None of those exists in the port, so
raised events were classified and delivered nowhere, and a first-request
failure could be invisible in the place a modern operator looks — the host's
log pipeline. Evidence and the options survey live in
[the research note](../research/portable-request-diagnostics-alternatives.md).

The decision: diagnostics leave the runtime through one choke point,
`WebFormsRuntimeEventSource`, which publishes every event on two channels —
the `EventSource` itself (string payloads, reachable out-of-process via
EventPipe with no host attached) and `Microsoft.Extensions.Logging.ILogger`
(live `Exception` objects, category `Rehost.WebForms.Runtime`, delivered into
the host's pipeline). Neither channel replaces the other: `EventSource` is the
only one that works with no host and during broken bootstrap; `ILogger` is the
only one operators and exception-telemetry sinks actually consume. This is the
shape ASP.NET Core hosting itself uses for the same conditions.

The choke point's `[NonEvent]` face takes exceptions as objects and is the
single caller of both channels, so they cannot drift; the `[Event]` methods
keep string payloads because `EventSource` serializes even in-process, which
is also why an `EventListener`→`ILogger` bridge was rejected: it can only ever
hand `ILogger` a string, and exception-object fidelity is a requirement.
The `ILogger` forward never throws — a failing log provider must not
recursively fail the request it is reporting on.

## Dependency and intake

The runtime references `Microsoft.Extensions.Logging.Abstractions` — its
first external package, accepted deliberately. The seams that avoided it (a
runtime-owned sink interface, in typed and single-generic-method variants)
reduce to re-implementing `ILogger` minus category filtering, `LoggerMessage`
source generation, and ecosystem familiarity, while every supported host
already carries the abstractions via `Microsoft.AspNetCore.App`.

The host hands over an `ILoggerFactory` as an explicit option on the existing
`WebFormsApplication.Initialize` intake, default `NullLoggerFactory`. Loggers
are created once and cached in statics: `ILogger` is singleton-shaped, and
logging scopes and trace ids are ambient, so a static logger inherits the
host's per-request enrichment whenever the call runs on the request's
execution flow. The runtime does not hold an `IServiceProvider`: a container
reference is service location — dependencies vanish from signatures,
root-vs-scoped lifetimes trap, and a missing registration degrades silently
instead of failing near the source. Future host inputs extend the options
type. If application-code DI ever becomes a compatibility requirement, its
surface is `HttpRuntime.WebObjectActivator`, a separate feature.

## Out of scope

Health monitoring stays a compatibility surface, not the diagnostics
foundation: the three native providers (Event Log, WMI, IIS trace) are
unsupported, and the managed provider model stays backlog until a real
application configures one. Metrics remain on the already-decided
`System.Diagnostics.Metrics` path behind `IPerfCounters`; an `ActivitySource`
for request tracing (the FREB analogue) is a separate future decision.
Redaction policy for exception text is open — Framework wrote full stack
traces to the event log, and filtering belongs to the host's logging pipeline.
