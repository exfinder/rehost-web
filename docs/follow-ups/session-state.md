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
  comment. Reading S5 measures the property against a running 4.8.1.
- It is also inert on the supported path. `SessionIDManager.InitializeRequest`
  returns `supportSessionIDReissue = false` whenever `Cookieless ==
  UseCookies`, above Microsoft's own note — *"We support cookie reissue only if
  we're using cookieless. VSWhidbey 384892"* (`SessionIDManager.cs:266-277`).
  Reissue is a cookieless-only feature, so nothing on the cookie path exercises
  it.

## Framework baseline

Rig: `winbox`, IIS Express 10.0.26013, `System.Web` **4.8.9344.0**
(`NET481REL1LAST_25H2_B`), a scratch site carrying **no `<sessionState>`
element** so the shipped defaults are what answer. Taken 2026-08-15.

Two mechanisms below were confirmed against the shipped binary with `ilspycmd`
rather than the pinned snapshot, because the snapshot alone could not settle
them.

### Effective defaults

| # | Reading |
| --- | --- |
| S1 | `mode=InProc` |
| S2 | `cookieName=ASP.NET_SessionId` |
| S3 | `cookieless=UseCookies` |
| S4 | `timeout=20` minutes |
| S5 | `regenerateExpiredSessionId=True` — the property, not the stale comment |
| S6 | `useHostingIdentity=True` |
| S7 | `cookieSameSite=Lax` |

### Cookie shape

| # | Reading |
| --- | --- |
| S8 | `Set-Cookie: ASP.NET_SessionId=<24 chars of [a-z0-9]>; path=/; HttpOnly; SameSite=Lax`. No `Expires` (browser-session cookie), no `Secure`, no `Domain` |
| S9 | A request already carrying a valid session cookie gets **no** `Set-Cookie` back |

### When the cookie is issued — gated by `Session_Start`

The decisive finding, and not the one the source reading predicted. Issuance
does not turn on whether the application writes to `Session`; it turns on
whether `Global.asax` **declares a `Session_Start` handler at all**.

| # | Reading |
| --- | --- |
| S10 | No `Session_Start` handler, page never touches `Session` → **no cookie** |
| S11 | No handler, page reads `Session["v"]` (null) → **no cookie** |
| S12 | No handler, page reads `Session.SessionID` → an ID is returned but **no cookie**; two successive cookie-less requests return **different** IDs |
| S13 | No handler, page writes `Session["v"]` → cookie issued, `IsNewSession=True` |
| S14 | No handler, a cookie-less request **after** another session was created and stored in the same process → still **no cookie**. Session creation is not sticky process-wide |
| S15 | `Session_Start` handler present that never touches `Session` → **every** session-enabled request issues a cookie, including the never-touch, read-item and read-ID pages, and the handler fires for each |

Mechanism (`SessionStateModule.cs:1285-1298`, `OnReleaseState`): a brand-new
session with nothing stored is discarded — *"Not storing unused new session"* —
and one of the four conditions for discarding it is
`_sessionStartEventHandler == null`. Declaring the handler therefore promotes
every request to a stored session. S12's per-request throwaway ID is the same
rule seen from the other side.

### Round-trip, identity, unknown IDs

| # | Reading |
| --- | --- |
| S16 | Second request under the cookie: same `SessionID`, `IsNewSession=False`, value readable, `Count` reflects stored items |
| S17 | On a live session: `Mode=InProc`, `IsCookieless=False`, `Timeout=20`, `IsReadOnly=False` |
| S18 | A syntactically valid but **unknown** ID in the cookie is **adopted**: `SessionID` echoes the client-supplied value, `IsNewSession=True`, `Count=0`, no `Set-Cookie`, and `Session_Start` fires for that ID |

### Opting in and out

| # | Reading |
| --- | --- |
| S19 | `EnableSessionState="false"`: `HttpContext.Current.Session` is null and `Page.Session` throws `HttpException` |
| S20 | Plain `IHttpHandler`: `HttpContext.Session` is null |
| S21 | `IRequiresSessionState` handler: session present, `IsReadOnly=False`, writes persist |
| S22 | `EnableSessionState="ReadOnly"` page: `IsReadOnly=True`, reads work, and a write **does not throw** |
| S23 | That read-only write **persists** to the next request |
| S24 | `IReadOnlySessionState` handler behaves the same as S22/S23 |

S23 is InProc-specific and worth stating plainly: the store hands out a live
reference to the cached collection, so mutating it during a read-only request
sticks without any save. The module never calls `SetAndReleaseItemExclusive` on
a read-only request, so an out-of-process or custom store would discard the same
write. Anything asserting "read-only means the write is lost" is asserting
out-of-proc behavior, not InProc.

### Request serialization

Two overlapping requests, each holding for 3 seconds, timestamps recorded by the
pages themselves.

| # | Reading |
| --- | --- |
| S25 | Same session cookie, writable pages: **strictly serialized** — the second entered 0.14 s *after* the first exited |
| S26 | Control, different session cookies: **overlapped** — the second entered 0.10 s after the first, while it still held |
| S27 | Same session cookie, both `EnableSessionState="ReadOnly"`: **overlapped**, 0.075 s apart. Read-only acquires are shared |

S26 is what makes S25 mean anything: it rules out the host simply being
serial. S27 pins the read-only path as genuinely non-exclusive rather than
merely faster.

### `Session_End`

| # | Reading |
| --- | --- |
| S28 | `Session.Abandon()` within the request: `SessionID` unchanged, `Count` still readable and unchanged for the rest of that request, no `Set-Cookie` |
| S29 | Next request under the same cookie: the **same** `SessionID` is reused with `IsNewSession=True`, `Count=0`. Abandon neither rotates the ID nor clears the cookie |
| S30 | `Session_End` fires on the abandon path with **`HttpContext.Current == null`** |

S30 corrects an assumption this plan started from. The abandon path was expected
to raise `Session_End` on the request thread with a live context; it does not.
So the cheap abandon-driven test does exercise the no-context condition, and the
expiry path's remaining unknown is narrower than "does it work without a
context" — it is whether the cache's own sweep thread reaches the callback at
all.

The sweep interval is not a guess either: `CacheExpires` in the shipped binary
carries `_tsPerBucket = 20 seconds` over `NUMBUCKETS = 30`. With the 1-minute
floor on `<sessionState timeout>`, a timer-driven `Session_End` test costs 60-80
seconds, which is why it stays unassessed here.

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

The three do not carry equal risk, and recording them as one line would say they
do. `UseUri` is mechanical URL munging. `AutoDetect` and `UseDeviceProfile`
decide from `Request.Browser`, and this port does not ship Framework's browser
capabilities — the root configuration substitutes `HttpCapabilitiesBase` for
`MobileCapabilities`, the definitions come from the compiled-in factory, and
`App_Browsers` is unsupported. Those two therefore rest on a divergence already
recorded, not merely on untested code.

**`Session_End`** — the abandon path is delivered; expiry stays unassessed.
InProc raises the event from a `CacheItemRemovedCallback`
(`InProcStateClientManager.cs:34,85`) for both removal reasons, so
`Session.Abandon` drives it as expiry would.

Reading S30 removed the reason to treat the abandon path as the easy half: it
already fires with `HttpContext.Current == null`, so the no-context condition is
covered. What expiry adds is the cache's own sweep thread reaching the callback
— territory where this port has previously found divergence (ledger P44, P46).
The machinery is present: the shipped binary's `CacheExpires` runs a real
`Timer` over 20-second buckets, and `AspNetCache` is what the port compiles
(`MemCache` sits behind `#if USE_MEMORY_CACHE` and is absent from the shipped
4.8.1 assembly too, so it is dead on both sides).

The cost of assessing it is the reason it stays open: `<sessionState timeout>`
has a 1-minute floor and the sweep runs on 20-second buckets, so the test is
60-80 seconds on every platform.

## Delivered

`Session` is registered in the shipped root `<httpModules>` at Framework's
position, preflight refuses both out-of-process modes, and the three evidence
claims are covered by `SessionStateOverKestrelTests` and
`CustomSessionStoreOverKestrelTests` over the `session` and `session-custom`
fixtures.

Two decisions departed from the plan above and are recorded rather than left
contradicted:

- The compatibility map carries **one** consolidated `Partial` row, not a row
  per mode. The boundaries are all stated inside it.
- The delivered evidence sits below the standing-test bar wherever it names no
  port-owned seam, per rung 0 of
  [writing tests](../writing-tests.md). `EnableSessionState="false"`,
  `IsNewSession`, the lazy no-`Session_Start` issuance path, and the cookie's
  own shape are Framework readings above plus compatibility boundaries; the
  standing tests keep only the registration, the exclusive acquire, the
  `ReadOnly` directive's codegen, the abandon-driven `Session_End`, the
  provider resolution, and the two refusals.

## Still open

- `Session_End` on expiry, which needs the cache sweep thread and a 60-80
  second test.
- Cookieless identity, split by risk as recorded above.
- `mode="SQLServer"` and `mode="StateServer"`, each owned by its own document.
