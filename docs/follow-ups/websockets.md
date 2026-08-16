# WebSockets

Done: ledger P80. `AcceptWebSocketRequest` runs on the classic pipeline over
ASP.NET Core's WebSocket middleware; the accept, handshake headers,
sub-protocol, same-origin, close handling, and callback context match the IIS
+ Framework readings (R-WS1–R-WS6 in
[the research](../research/websockets-and-streaming.md)).

## Recorded boundaries

- Body bytes written after `AcceptWebSocketRequest` are dropped; IIS wrote them
  raw between the 101 and the first frame (R-WS2), which no client parses.
- `AspNetWebSocketContext.User` is null on the shipped module set where IIS's
  `DefaultAuthenticationModule` supplied an anonymous principal — the
  [shipped-modules follow-up](shipped-http-modules.md) owns that.
- The integrated-only "cannot be called after the handler executed" ordering
  check is not carried; the BeginRequest refusal is.
- Kestrel's `WebSocketOptions` (keep-alive interval, allowed origins) are the
  host's to configure; Framework had no equivalent surface.
