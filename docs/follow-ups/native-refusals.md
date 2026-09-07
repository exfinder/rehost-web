# Native refusals

Design for approval. Not landed.

## Problem

IIS answers a missing static file with a native 404: a response, not a managed
exception. The port's `StaticFile` bridge refuses the same request by throwing
`HttpException(404)` at handler mapping, so every application's
`Application_Error` runs for it, and `customErrors` is consulted. YAF stores each
error application-wide and renders it on `error.aspx`; with DevTools open,
Chrome's `/.well-known/appspecific/com.chrome.devtools.json` probe replaced the
real error on every refresh.

## Readings

[MH40 and MH41](../research/iis-modules-handlers-readings.md), IIS Express 10 /
4.8, 2026-09-07:

| | IIS | port today |
| --- | --- | --- |
| `Application_Error` for a missing `.json` | never | always |
| `customErrors` 404 row applied to it | no | yes |
| `BeginRequest` status | 200 | 200 |
| `LogRequest`, `EndRequest`, `PreSendRequestHeaders` status | 404 | 404 after the error page |
| body | IIS's 404 page, no `X-AspNet-Version` | ASP.NET's 404 page |
| missing `.aspx` | managed 404, `Application_Error` fires, `customErrors` applies | same |

## Contract

1. A refusal a native IIS module owned is a response. `context.Error` stays
   null, `Server.GetLastError()` returns null, `Application_Error` does not run.
2. `customErrors` is not consulted. `httpErrors` has no port counterpart; the
   body is a fixed IIS-shaped page, not IIS's bytes.
3. The refusal is decided where IIS decided it, the handler stage, so every
   event before it runs unchanged and `LogRequest` onward see the status.
4. A managed miss (`/nosuch.aspx`) keeps Framework's exception, page, and
   `customErrors`.

## Shape

One internal handler, `NativeRefusalHandler(status, reason)`, in
`Compatibility/IisConfig`. Its `ProcessRequest` clears the response, sets the
status, writes the fixed body, and calls `CompleteRequest()`, so the step
manager jumps to `LogRequest` with no error on the context.

`IntegratedHandlers.Map` returns it instead of throwing where a native row
refuses: `RequireResource` (`resourceType` miss, MH26 and MH40) and the no-match
404. `StaticFileBridgeHandler` answers its own three refusals the same way:
the extension-gate 404, the classic-ASP 403, and the POST 405. The 403 and 405
ride along because they are the same module's responses on IIS (IV29 for the
verb case); they are not separately measured for `Application_Error`.

The refusal reports once on the diagnostics channel at information level,
naming the route and both paths, since IIS wrote a log line and the port
otherwise leaves no trace.

## Out of this cut

- Request filtering (`HiddenSegments`, `ForbiddenExtensions`) refuses in
  `ValidatePathExecutionStep`, ahead of `BeginRequest`, as IIS does natively.
  Whether managed events fire after a native 404.7/404.8 is unmeasured; a reading
  (MH42) decides whether the same handler shape or an earlier end applies.
- A missing directory: IIS answers through `ExtensionlessUrlHandler` and a
  `TransferRequest` child (MH41); the port's own step owns it (P67).

## Impact

- YAF: the probe's 404 no longer reaches `Application_Error`; `error.aspx`
  shows the real error.
- Wingtip Toys: its `customErrors` 404 claim is for `/NoSuchPage.aspx`,
  managed; unchanged.
- Existing scenarios assert status only for these refusals; the status does
  not change. A body assertion, if any, moves to the fixed page.

## Evidence to add

- Scenario on a fixture whose `Global.asax` records `Application_Error`:
  `GET /nosuch.json` is 404, the body is the fixed page, no error was recorded,
  and a traced request shows 404 at `LogRequest`; `GET /nosuch.aspx` still
  records one. Red today on the first three assertions.
- Ledger row: native refusals are responses. Compatibility: the static-files
  boundary added on 2026-09-07 comes off; the `customErrors` row gains the
  native-404 boundary.
