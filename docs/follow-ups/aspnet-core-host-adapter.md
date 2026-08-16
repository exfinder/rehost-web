# ASP.NET Core host adapter gaps

The implemented boundary is recorded in
[the host ADR](../adr/0003-host-boundary.md) and
[compatibility map](../compatibility.md). This file owns only residual work.

## Remaining work

Ledger P72 and P76–P78 closed the path-info split, the server-variable
inventory, request scheme/host, forwarded headers, both header encodings, the
response-spill tests, HTTP/2, the long-file flag, and recorded WebSockets
([follow-up](websockets.md)) and client certificates as unsupported
([research](../research/host-adapter-residuals.md)).

- Client-visible streaming: a mid-request `Flush` reaches the client only at
  the single commit after `EndOfRequest` (ADR 0003); a scenario asserting
  first-flush timing is the next step, and honoring it means committing headers
  at the first flush and streaming the rest.
- HTTP/3: no transport gate.
- Compression is IIS's module and its configuration; on the
  [IIS-role follow-up](iis-role-behaviors.md).
- Client certificates behind a proxy: forwarded-certificate header into `CERT_*`
  when a consumer needs it.
- Integrated-mode server variables (`UNENCODED_URL`, `HTTP_URL`, native-module
  values) and `ServerVariables.Set`: `HttpServerVarsCollection` reaches only an
  `IIS7WorkerRequest` for them; unassessed.

## Done when

Every adapter member reached by a milestone application either preserves its
transport contract or fails explicitly, and response spill/cleanup cannot pass
without exercising disk.
