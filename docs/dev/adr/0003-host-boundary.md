# Host boundary

This is the accepted target interface. Current hosting still exposes static
bootstrap plus an internal activation service; alignment remains in the
[backlog](../backlog.md).

## Decision

Expose one deep host-neutral application interface: create one application from
explicit options, submit `HttpWorkerRequest` instances asynchronously, and stop
the application. ASP.NET Core translates transport state and relays lifecycle;
it does not own System.Web activation or object lifetimes.

Call `HttpRuntime.ProcessRequest` directly. Do not wrap entry in `Task.Run` and
do not use `ProcessRequestNow`. The adapter awaits exactly-once worker-request
completion.

System.Web owns module, handler, and application failures once it can format the
response. Translation, transport, commit, or pre-pipeline escapes are adapter
failures. A disconnect is exposed to System.Web but does not abandon a pipeline
that may still access request state.

Responses spool status, headers, and memory/file fragments, and are committed
incrementally: a mid-request flush that follows System.Web's own header
generation publishes the head once and every fragment collected since the last
flush, on the pipeline thread waiting for the transport (the request-body trade
in the other direction); `EndOfRequest` seals
managed output, then the adapter commits what remains asynchronously. A flush
before the head exists publishes nothing. Kestrel synchronous I/O stays disabled.

## Consequences

- The runtime assembly has no ASP.NET Core dependency.
- Runtime-owned assemblies resolve normally; one immutable fallback resolver
  probes the application `bin` directory and generated output.
- Initialization error pages are not replaced with generic host errors.
- File sending and streaming grow through explicit worker-request capabilities,
  not silent empty implementations.
- Request-body thread-pool tradeoffs are recorded separately in
  [request-body threading](0006-request-body-threading.md).
