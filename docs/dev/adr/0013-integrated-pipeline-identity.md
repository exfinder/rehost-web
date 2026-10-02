# Integrated-pipeline identity

## Decision

`HttpRuntime.UsingIntegratedPipeline` returns `true`; `IISVersion` returns `10.0`,
matching the IIS-derived configuration model. Internal `UseIntegratedPipeline`
stays `false` because execution enters the classic managed
`HttpRuntime.ProcessRequest(HttpWorkerRequest)` engine.

## Rationale

The public properties select third-party feature branches for an integrated
application pool. Returning classic identity would select behavior different
from the application being migrated. The internal flag selects native IIS
notification plumbing through IIS7WorkerRequest, which this host does not have.
AppVerifier uses the internal flag because its NotificationContext assertion
belongs to that native plumbing.

## Consequences

- Integrated callers still need host-neutral seams. Katana subscribes to
  StopListening and reads WEBSOCKET_VERSION; disconnect notification comes from
  ClientDisconnectedToken. Its response-compression fast path requires an actual
  IIS7WorkerRequest before invoking a delegate that casts to that type; other
  workers use writable Request.Headers.
- Integrated identity does not promise native IIS execution. Host concurrency
  properties refuse actionably; optional WebSocket/abort seams may refuse on a
  host that does not implement them. Silent classic fallback is unacceptable.
- The managed step list supplies mapping/log events, notifications and pre-send
  behavior; exact supported boundaries belong to [compatibility](../compatibility.md).
- Original native version fields remain available to Framework-only code.
