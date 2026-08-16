# ASP.NET Core host adapter gaps

The implemented boundary is recorded in
[the host ADR](../adr/0003-host-boundary.md) and
[compatibility map](../compatibility.md). This file owns only residual work.

## Remaining work

- Exercise the response-spill path with a body above the in-memory threshold,
  including cleanup after success, disconnect, and commit failure.
- Inventory server variables needed by representative applications. IIS-only
  variables must be translated, rejected, or explicitly left unassessed.
- ~~Derive `PathInfo` and the file/path-info split without filename heuristics~~ — done, IIS's handler-mapping rule (ledger P72).
- Translate `<globalization responseHeaderEncoding>` and determine non-ASCII
  request-header decoding. The current host pins Framework's UTF-8 response
  default.
- Define client-certificate, compression, and protocol-upgrade/WebSocket
  behavior instead of inheriting empty worker-request defaults.
- Gate real HTTP/2 and HTTP/3 request bodies and completion.
- Define public streaming and file-send behavior beyond the internal known-file
  paths already used by static serving.

## Done when

Every adapter member reached by a milestone application either preserves its
transport contract or fails explicitly, and response spill/cleanup cannot pass
without exercising disk.
