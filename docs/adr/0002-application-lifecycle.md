# Application lifecycle

This is the accepted target. Current static bootstrap, lazy activation, and
host-initiated shutdown are documented in
[application bootstrap](../application-bootstrap-and-configuration.md); the
implementation gap remains in the [backlog](../backlog.md).

## Decision

One process-scoped application owner controls registration, activation, request
admission, shutdown notification, and final stop.

Registration validates immutable explicit host options without mutating
System.Web global state. The first routed request single-flights activation and
waits outside System.Web. Request-independent hosting startup completes before
the same real request enters `HttpRuntime`; request-dependent first-request
initialization, `Global.asax`, and `Application_Start` retain Framework timing.

The lifecycle is:

`Registered → Activating → Accepting → StopRequested → Stopped`

An escape after legacy global mutation makes the generation terminal. Managed
request failures remain owned by System.Web. Configuration, binaries, content,
and generated state are immutable after publication; replacement requires a new
process.

## Consequences

- One application and one `HostingEnvironment` exist per process/current
  AppDomain.
- `ApplicationManager` remains an internal current-AppDomain lifecycle adapter;
  child AppDomains, remoting, unload, and multi-application management do not.
- An imported shutdown request emits one idempotent notification. The ASP.NET
  Core host calls `StopApplication`; runtime code does not call
  `Environment.Exit` or reinitialize in place.
- A host-owned writable compilation root is explicit; codegen storage details
  are in [codegen storage](0008-codegen-storage.md).
