# Session state

Scope: `sessionState` `mode="InProc"` and `mode="Custom"`. The other two modes
and the shipped module list are owned elsewhere: [SQL mode](session-sql.md),
[state server mode](session-state-server.md),
[shipped HTTP modules](shipped-http-modules.md).

## Why nothing works today

The implementation is imported and compiled — `State/`, 7,959 lines, none of it
removed by the runtime project (`Rehost.WebForms.Runtime.csproj:17-30`). Nothing
runs it. The shipped root web configuration carries `<httpHandlers>`
(`configs/rehost-webforms.web.config:107-157`) and **no `<httpModules>`
collection at all**, so `SessionStateModule` is never registered and
`HttpContext.Session` is null on every request. Framework's root configuration
registers it (`third_party/microsoft/framework-config/web.config:231`). Only the
section handlers are declared here
(`configs/rehost-webforms.machine.config:28,40-41`).

An application configured for session state therefore gets silence, which the
project contract rules out.

## Scope

In:

- registering `Session` in the shipped root `<httpModules>`;
- `mode="InProc"`: the cache-backed store, session identity over the
  `ASP.NET_SessionId` cookie, the 20-minute sliding timeout;
- `mode="Custom"`: the `SessionStateStoreProviderBase` seam and the call
  sequence the module drives it with;
- `IRequiresSessionState` / `IReadOnlySessionState` and the `EnableSessionState`
  page directive, including the read-only path's non-exclusive acquire;
- `Session.Abandon`, and the request serialization the exclusive acquire
  produces;
- activation refusal for the two modes this story does not deliver.

Out:

- `mode="SQLServer"` and `mode="StateServer"`, each with its own document;
- cookieless session identity, and `Session_End`, both recorded as unassessed
  below;
- session state in a farm shared with Framework nodes, which needs an
  out-of-process store and belongs to whichever store story lands first.

## Defaults that decide the tests

- `cookieless` defaults to `UseCookies` (`SessionIDManager.cs:79`), so cookieless
  identity is opt-in and never reached by accident.
- Cookie name `ASP.NET_SessionId` (`SessionIDManager.cs:80`); timeout 20 minutes
  (`SessionStateModule.cs:121`); session ID length limit 80 characters.
- `regenerateExpiredSessionId` really defaults to **true**
  (`SessionStateSection.cs:183-185`). The imported comment block at
  `SessionStateSection.cs:59-61` documents `false`; it is Framework's own stale
  header and the property wins. Anything asserting the documented value tests the
  comment.

## Refusing the modes this story does not deliver

Activation preflight already opens the merged web configuration and refuses
`trust` and `fcnMode` before the application starts
(`ApplicationBootstrap.cs:342-404`); `<sessionState>` is
`allowDefinition="MachineToApplication"`, so the merged application
configuration is exactly where it is visible. Both refusals go there.

The two wordings stay distinguishable, because one of them may never change:

- `StateServer` — *is not supported*;
- `SQLServer` — *is not yet implemented*.

Each names the configured mode and the modes that work. Neither names a
document: diagnostics that cite planning artifacts rot and mean nothing to a
consumer.

The cost is accepted deliberately: an application whose configuration names a
mode it never exercises starts on Framework and refuses here. `trust` and
`fcnMode` already refuse on the same terms.

## Evidence

Three claims, all supported platforms. None needs a container, a service, or a
tunnel, so nothing here is skipped.

1. **Round-trip.** A value written during one request is read during the next
   under the same session cookie.
2. **Request serialization.** Two overlapping requests carrying one session ID
   are observed strictly sequential, never concurrent. This is what the
   exclusive acquire exists for, it is the session behavior applications
   actually notice, and no in-memory shortcut passes it by accident. It needs a
   deliberately slow page and enough margin that ordinary scheduling noise
   cannot fake the overlap.
3. **Provider call sequence.** A recording `SessionStateStoreProviderBase` in the
   fixture pins what the module drives: `GetItemExclusive` then
   `SetAndReleaseItemExclusive` for a writable page, a shared `GetItem` for a
   read-only one, `ReleaseItemExclusive` on abandonment. This is what `Custom`
   mode *is*; a provider that round-trips values while ignoring the locking
   protocol must fail here.

A round-trip assertion alone would pass against a provider that ignores the
protocol entirely, so it is a smoke case rather than the evidence.

## Boundaries recorded, not built

**`useHostingIdentity`** — supported by degeneracy. It selects which identity
opens the store connection: `true` (the default,
`SessionStateSection.cs:195-197`) reverts client impersonation and connects as
the application's own identity; `false` connects as the impersonated caller.
Ledger P07 leaves impersonation inert here — a zero application identity token —
so there is never an impersonated caller, and both values connect as the process
identity. The compatibility map records that boundary rather than claiming
support. Whether explicit `false` should be refused travels with the
impersonation policy P07 already defers.

**Cookieless identity** — unassessed, not refused. `UseUri`, `AutoDetect`, and
`UseDeviceProfile` are pure managed code with no native or Windows call sites:
`CookielessHelper.cs` and `SessionIDManager.cs` carry zero `DllImport` or
`UnsafeNativeMethods` references, and the call sites — `HttpContext.cs:485`,
`HttpResponse.cs:3279-3341`, `HttpRequest.cs:2249` — are imported source
compiling today. Nothing structurally blocks them; they are simply untested.
Refusing code that may well work would invent a boundary the runtime does not
have.

**`Session_End`** — unassessed in both modes. The machinery is present and
compiled: InProc raises it from a `CacheItemRemovedCallback`
(`InProcStateClientManager.cs:34,85`), and for a provider that supports expiry
the module raises it explicitly on abandonment
(`SessionStateModule.cs:1308-1318`). The open question is whether the cache
expiration timer fires the eviction callback here, on a background thread with
no `HttpContext` — territory where this port has previously found divergence
(ledger P44, P46).

## Done when

- `Session` is registered in the shipped root configuration and an application
  observes a working `HttpContext.Session` with no configuration of its own.
- The three evidence claims pass on Windows x64, Linux x64, and macOS arm64.
- Preflight refuses `SQLServer` and `StateServer` with the wordings above, and a
  test fails if either refusal is removed.
- The compatibility map carries rows for both delivered modes, the two
  unassessed areas, and the two refusals.
