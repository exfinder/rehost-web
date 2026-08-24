# WebSockets and client-visible streaming over Kestrel

Evidence for supported streaming and WebSockets, recorded by ledger P79/P80
and [ADR 0003](../adr/0003-host-boundary.md).

## Result

System.Web already emitted correct chunked bytes; P79 made non-final
`FlushResponse` deliver committed spool segments after `SendStatus`. The guard
matters because `Response.End` can flush before headers exist. Async flush uses
the worker-request async seam; transport failure faults the spool and aborts.

P80 adds a worker-request upgrade seam over ASP.NET Core's WebSocket feature,
then drives the existing `AspNetWebSocket` callback through a Kestrel-backed
pipe. Imported argument checks, same-origin policy, negotiation, and callback
state remain authoritative. Accepted deltas: body bytes after accept are
dropped; callback `User` is null; the integrated-only post-handler ordering
check is absent.

## Framework readings

Captured 2026-08-16 on IIS 10 / Framework 4.8.9344 using raw sockets and
per-read timestamps.

| ID | Result |
| --- | --- |
| R-S1 | Flush sends headers and first chunk immediately; later chunks follow each flush; final zero chunk preserves keep-alive |
| R-S2 | `End` after flush sends pending bytes and terminator; later code emits nothing |
| R-S3 | `TransmitFile` plus following write may coalesce into one chunk |
| R-S4 | Error after flush keeps status 200 and appends the error page as a chunk |
| R-S5 | Header, status, and cookie mutations after flush throw; client remains connected |
| R-WS1 | 101 includes app cookie/headers written before and after accept, plus upgrade headers |
| R-WS2 | IIS writes body after accept raw between 101 and first frame; clients cannot parse it |
| R-WS3 | Callback has current context, items, origin, and WebSocket; response unavailable; session null; anonymous user present |
| R-WS4 | Requested subprotocol negotiates; an unavailable requested protocol throws |
| R-WS5 | `RequireSameOrigin` rejects absent/foreign origin and accepts matching origin |
| R-WS6 | Client- and server-initiated close handshakes preserve code/reason |

Kestrel tests cover timing, framing semantics, transport failure, negotiation,
origin policy, both close directions, and the documented callback deltas.
