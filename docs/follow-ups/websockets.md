# WebSockets

## Problem

`HttpContext.IsWebSocketRequest` and `AcceptWebSocketRequest` ride IIS's native
WebSocket module through `IIS7WorkerRequest`; every other worker request,
this port's adapter included, is refused with Framework's own
`PlatformNotSupportedException` ("This operation requires IIS integrated
pipeline mode"), pinned by `HttpContextWebSocketsTests` (ledger P78). SignalR
1.x/2.x and hand-written WebSocket handlers reach it.

## Required decisions

- Wire `AcceptWebSocketRequest` over Kestrel's `IHttpWebSocketFeature`: the
  adapter reports the upgrade request, the accept hands System.Web's
  `AspNetWebSocket` a Kestrel `WebSocket`, and the response commit path yields
  to the upgraded connection instead of the single sealed commit (ADR 0003).
- `IsWebSocketRequest` from the `Upgrade: websocket` handshake without a native
  module; the "cannot call from BeginRequest" ordering rule.
- The substitute meanwhile: ASP.NET Core's WebSockets middleware beside the
  port for new endpoints.

## Done when

A fixture handler accepts a WebSocket over Kestrel, echoes frames, and closes,
on all three platforms; the refusal test flips to the working path.
