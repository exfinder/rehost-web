# Request termination and timeouts

Status: done 2026-08-05, except the connection-abort follow-up below. The
termination half is owned by
[Response.End and request termination](response-end-and-termination-plan.md)
(ledger P52); the timeout half is delivered cooperatively (ledger P53) with
its boundaries in the
[compatibility map](compatibility-feature-map.md#request-termination).

## Delivered policy

Modern .NET cannot force-stop arbitrary synchronous user code, so
`executionTimeout` is enforced cooperatively. The 15-second scan (period
configurable via the port-owned `rehost:RequestTimeoutScanSeconds` appSettings
key) keeps Framework's guards — cancellable period, `ThreadAbortOnTimeout`,
`debug`/debugger suppression — cancels `Request.TimedOutToken` at budget, and
flags the request; the step-boundary checkpoint consumes the flag once and
unwinds through the termination recovery, producing Framework's
*"Request timed out."* `Application_Error` and 500.

Named boundary: a running step is never interrupted. The 500 arrives when the
current step returns — late for a single slow call, never for a step that never
returns. Async steps are not flagged; deferred with async pages.

## Follow-up: connection abort at budget

Owned here, not yet scheduled. Bounding the *client's* wait for a blocked step
requires the hosting layer to abort the connection when the budget expires —
the response never arrives, while the server-side step still runs to wherever
it runs. Framework precedent: it aborts the connection itself when a timed-out
thread is blocked in a synchronous entity-body read
(`HttpContext.MustTimeout`'s former `IsInReadEntitySync` arm, removed with the
abort handshake). Requires adapter machinery and abort-vs-flush race coverage;
take it up if the late 500 bites a real application.
