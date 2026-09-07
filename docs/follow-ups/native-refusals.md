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

[MH40, MH41 and MH42](../research/iis-modules-handlers-readings.md), IIS Express 10 /
4.8, 2026-09-07:

| | IIS | port today |
| --- | --- | --- |
| `Application_Error` for a missing `.json` | never | always |
| `customErrors` 404 row applied to it | no | yes |
| `BeginRequest` status | 200 | 200 |
| `LogRequest`, `EndRequest`, `PreSendRequestHeaders` status | 404 | 404 after the error page |
| body | IIS's 404 page, no `X-AspNet-Version` | ASP.NET's 404 page |
| missing `.aspx` | managed 404, `Application_Error` fires, `customErrors` applies | same |
| hidden segment / forbidden extension (404.8 / 404.7) | no `BeginRequest`, no error event, Log/End/PreSend 404, `customErrors` ignored | no `BeginRequest`, **`Application_Error` fires**, then the same events |

## Contract

1. A refusal a native IIS module owned is a response. `context.Error` stays
   null, `Server.GetLastError()` returns null, `Application_Error` does not run.
2. `customErrors` is not consulted. `httpErrors` has no port counterpart; the
   body is a fixed IIS-shaped page, not IIS's bytes.
3. The refusal is decided where IIS decided it: the handler stage for a
   missing file, `ValidatePathExecutionStep` (ahead of `BeginRequest`) for
   request filtering. Every event before it runs unchanged, none is added, and
   `LogRequest` onward see the status.
4. A managed miss (`/nosuch.aspx`) keeps Framework's exception, page, and
   `customErrors`.

## Shape

One internal entry point, `NativeRefusal.Respond(context, status, reason)`, in
`Compatibility/IisConfig`: clears the response, sets the status, writes the
fixed body, and calls `CompleteRequest()`, so the step manager jumps to
`LogRequest` with no error on the context. Two callers:

- `ValidatePathExecutionStep`: `HiddenSegments` and `ForbiddenExtensions`
  call it and return instead of throwing; the step manager's post-step check
  takes the jump, and `BeginRequest` never runs, as measured.
- `NativeRefusalHandler(status, reason)`, a handler whose `ProcessRequest` is
  that call, for refusals decided at mapping or inside the static handler.

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

- `Global.asax` events on a native request without
  `runAllManagedModulesForAllRequests`: IIS raises none (MH41, MH42), the port
  raises Log/End/PreSend. A `managedHandler` precondition matter (P83), listed
  in the backlog, not a refusal matter.
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
  `GET /nosuch.json`, `/bin/x.dll` and `/web.config` are 404, the body is the
  fixed page, no error was recorded, and a traced request shows 404 at
  `LogRequest` with no `BeginRequest` for the filtered two; `GET /nosuch.aspx`
  still records one. Red today on the error-recorded assertions.
- Ledger row: native refusals are responses. Compatibility: the static-files
  boundary added on 2026-09-07 comes off; the `customErrors` row gains the
  native-404 boundary.
