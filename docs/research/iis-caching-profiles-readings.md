# IIS `system.webServer/caching` readings

Full IIS 10 on winbox, 2026-09-12, scratch site `cache1` (static content only,
default app pool, anonymous). Root `web.config` declared one profile per
extension; `sub/web.config` added `.txt`, replaced `.css`, and inherited the
rest; `clr/web.config` carried `<profiles><clear /></profiles>`. Headers read
with `curl.exe` on the box; two remote requests from the Mac matched byte for
byte. Golden `applicationHost.config` declares the section
`overrideModeDefault="Allow"` and carries `<caching enabled="true"
enableKernelCache="true">` with no profiles.

| # | Profile | `Cache-Control` | Other cache headers |
| --- | --- | --- | --- |
| CP1 | none (`.txt`) | absent | `Last-Modified`, `ETag`, `Accept-Ranges` only |
| CP2 | `CacheUntilChange`, `location="Client"`, `varyByHeaders="Browser"` (`.css`) | `private` | no `Vary`, no `Expires`; same with a different `User-Agent` and with `?x=1` |
| CP3 | `CacheForTimePeriod` `00:10:00`, `location="Any"` (`.js`) | `public` | no `Expires`, no `max-age` |
| CP4 | `kernelCachePolicy="CacheUntilChange"` only, `location="Client"` (`.png`) | absent | as CP1; the kernel policy alone adds nothing to the wire |
| CP5 | `CacheUntilChange`, `location="Server"` (`.gif`) | `no-cache` | |
| CP6 | `DisableCache` (`.htm`) | absent | as CP1 |
| CP7 | `CacheUntilChange`, `location="Downstream"` (`.svg`) | `public` | |
| CP8 | `CacheUntilChange`, `location="ServerAndClient"` (`.ico`) | `private` | |
| CP9 | `CacheUntilChange`, `location="Any"` (`.map`) | `public` | |
| CP10 | nested `sub/web.config` adds `.txt` `Client` | `private` | a folder file defines profiles |
| CP11 | nested `remove` + `add` `.css` `CacheForTimePeriod` `Client` | `private` | a folder file replaces an inherited profile |
| CP12 | nested folder, inherited `.js` (CP3) | `public` | inherited profiles apply below |
| CP13 | nested `<clear />`, then `.css`, `.js`, `.txt` | absent | a folder file drops every inherited profile |
| CP14 | conditional `If-Modified-Since` on CP3 | `public` on the 304 | |
| CP50 | no profile: `Range: bytes=2-6`, an unsatisfiable range, `HEAD`, and `If-Modified-Since` | 206, 416, 200 and 304 all carry no `Cache-Control` and no `Expires`; `Last-Modified`, `ETag` and `Accept-Ranges` on each except the 304, which carries `ETag` and `Accept-Ranges` only | |
| CP51 | `Client` profile: `Range: bytes=2-6` and `If-Modified-Since` | `private` on the 206 and on the 304; the profile's word rides every static status | |

Effective configuration read back through `Get-WebConfiguration` for `sub`
listed nine profiles: the two folder entries and seven inherited; `clr` listed
none.

Scope: these rows measure static files only, so they show what a profile puts
on the wire and nothing else. Microsoft's reference for the section describes
it as the IIS output cache: a dynamic response (`.asp`, `.aspx`, any handler
output) is stored in memory after the first request and served again without
re-processing the page, in user mode, kernel mode, or both; `duration`,
`varyByHeaders` and `varyByQueryString` shape that stored copy's lifetime and
key. A static file renders the same bytes either way, so the rows below cannot
see that effect; the dynamic rows further down do.

Observations (static files):

- `location` alone decides the response header: `Client` and `ServerAndClient` are
  `private`; `Any` and `Downstream` are `public`; `Server` is `no-cache`;
  `None` unmeasured.
- `policy` decides whether a header is sent at all: `DontCache` and
  `DisableCache` send nothing; `CacheUntilChange` and `CacheForTimePeriod`
  send the location's word. `duration` never reaches the wire as `Expires`
  or `max-age`.
- `varyByHeaders` and `varyByQueryString` never reach the wire as `Vary`.
- Nested `web.config` files merge like any IIS collection: `add`, `remove`,
  `clear`, inherited entries apply to the folder's requests.
- Not measured: a managed handler's `.axd` under a kernel-only profile
  (YAF's `.axd` row), `location="None"`, and whether `enableKernelCache`
  changes a second response.

IIS Express cannot take these readings: its `applicationhost.config` declares
the `caching` section but ships no `HttpCacheModule`, and a control site there
stored nothing. Every dynamic row below is full IIS 10.

## Dynamic content

Same box, same day, scratch site `cache2`: an ASP.NET 4.8 integrated pool,
`Counter.aspx` and `Counter.ashx` writing a static per-class counter and
`DateTime.UtcNow.Ticks`, one copy per folder, each folder's `web.config`
clearing the inherited profiles and declaring its own. `duration` was five
seconds throughout.

| # | Profile | Handler runs? | `Cache-Control` |
| --- | --- | --- | --- |
| CP15 | none | every request | `private` (ASP.NET's own) |
| CP16 | `.aspx` `CacheForTimePeriod` `00:00:05` (default `location="Server"`) | first and second request run; the third and later return the second's bytes until five seconds pass, then one runs and the cycle repeats | `no-cache` |
| CP17 | `.ashx`, same profile | as CP16 | `no-cache` |
| CP18 | CP16 with `?a=1`, `?a=2`, `?a=1` and no `varyByQueryString` | one stored copy serves every query string | `no-cache` |
| CP19 | `varyByQueryString="id"`: `?id=1` x2, `?id=2` x2, `?id=1`, `?other=9` x2 | one copy per `id` value; a request without `id` is its own copy | `no-cache` |
| CP20 | `varyByHeaders="User-Agent"`: A, A, B, B, A | one copy per `User-Agent` value | `no-cache` |
| CP21 | `kernelCachePolicy="CacheForTimePeriod"` only, no user-mode `policy` | cached from the third request like CP16 | `private` (ASP.NET's own; the kernel copy carried it) |
| CP22 | CP16 with `location="Any"` | cached like CP16 | `public` |
| CP23 | CP16 with `location="Client"` | cached like CP16; `Client` does not turn the server copy off | `private` |
| CP24 | `CacheUntilChange` on `.aspx` and `.ashx` | cached like CP16; a `LastWriteTime` touch, and then appended content, still served the stored bytes two seconds later on five further requests; longer waits unmeasured | `no-cache` |
| CP25 | `POST` to CP16's page | every request runs; only `GET` is stored | none |
| CP26 | CP16 after twelve idle seconds | first runs, second runs and is stored, third and fourth return the second's bytes | `no-cache` |
| CP28 | Forms authentication, `/Open.aspx` with no `<authorization>`: bob, bob, anonymous, alice | the second bob response is stored and served to the anonymous client and to alice, body still reading `user=bob auth=True` | `no-cache` |
| CP29 | `/secure/Page.aspx` under `<deny users="?" />`: anonymous, bob x3, anonymous, alice, bob | anonymous gets 302 to the login page both times, stored copy or not; bob's first response is stored (the anonymous 302 counted as the first hit) and served to alice with `user=bob` | `no-cache` |
| CP30 | priming window: hit, 11 s idle, hit, hit, hit | the two hits 11 s apart do not prime; the next two consecutive hits do, and the fourth is served. After 32 s idle, two quick hits prime again. The window is about 10 s between consecutive hits, not from the first hit | `no-cache` |
| CP31 | body size: 300 KB page x4, 200 KB page x4 | 300 KB is never stored; 200 KB stores on the second hit. The default `maxResponseSize` of 256 KB holds | `no-cache` |
| CP32 | page calls `Response.Cache.SetNoStore()` under a profile | stored anyway on the second hit; the first two responses carry `private, no-store`, the served copy carries the profile's `no-cache` | see row |
| CP33 | page calls `SetCacheability(NoCache)` | stored anyway | `no-cache` |
| CP34 | page calls `SetCacheability(Private)` + `SetExpires` | stored anyway; served copy carries `no-cache`, not the page's `private` | see row |
| CP35 | page carries `@OutputCache Duration="60"` under a profile | ASP.NET's own cache answers from the second request with `public`; the profile's header never appears | `public` |
| CP36 | page adds its own cookie (`Response.Cookies.Add`) | never stored; each response carries its `Set-Cookie`; a later plain page in the same app still stores (isolation G) | `private` |
| CP37 | page writes `Session[...]` so ASP.NET issues `ASP.NET_SessionId` | never stored, and from then on no new copy is stored for any page in the application: a plain page hit 8 times over 96 s ran every time (isolation E, H, I). Copies stored before keep serving. Reading `Session` without writing (no cookie issued) and presenting a made-up session cookie do not trigger it (J, K). Recovery beyond 96 s unmeasured | `private` |
| CP38 | non-200 output: page throws (500), sets 404, sets 500, `Response.Redirect` (302) | none stored; every request runs | `private` on the 500 error page, `no-cache` on the rest |
| CP39 | `/ext/` served by default document `Default.aspx`, then `/ext/Default.aspx` | one copy: the directory request stores it under the rewritten `/ext/default.aspx`, and the explicit request is served from it | `no-cache` |
| CP40 | `/ext/Page.aspx/extra` (path info) | stored; the extension is taken from the file segment, not the URL's end | `no-cache` |
| CP41 | `/ext/PAGE.ASPX` | profile matches case-insensitively; stored | `no-cache` |
| CP42 | `/friendly`, an extensionless page route to `Page.aspx` | never stored; the profile is matched on the request URL's extension, not the handler's file | `private` |
| CP43 | `varyByQueryString="id"`: `?id=1&x=2`, `?x=2&id=1`, `?ID=1`, `?id=1&x=9` | `id=1&x=2` and `x=2&id=1` and `id=1&x=9` share one copy; `ID=1` is a second copy. The key is the named parameter's value; other parameters and order are ignored; the name is case-sensitive | `no-cache` |
| CP44 | `varyByQueryString="*"`: `?a=1`, `?a=2`, `?b=1&a=1`, `?a=1&b=1` | four copies: the key is the raw query string, order included | `no-cache` |
| CP45 | `<caching enabled="false">` in a folder file, with a profile | nothing stored in that folder; the header word still applies; other folders unaffected (isolation B) | `no-cache` |
| CP46 | `<caching enableKernelCache="false">` with a kernel-only profile | nothing stored; no header | `private` |
| CP47 | `location="None"` | stored like `Server` | `no-cache` |
| CP48 | `policy` and `kernelCachePolicy` both set, `location="Any"` | stored | `public` |
| CP49 | CP37 isolated with distinct files: `P1` x3, `NoSession` x3, one `Session` write, then `P2` x3 (new path), `P1` x2, `N2` x3 (new path, `EnableSessionState="False"`), 15 s idle, `P3` x3 | before the session: `P1` and `NoSession` store on the second hit. After: `P2` and `P3` never store; `P1`'s old copy still serves; `N2` stores. No response after the session carries `Set-Cookie` | `no-cache` throughout |
| CP27 | CP16 with a `Global.asax` logging every pipeline event | a stored hit runs `BeginRequest`, `AuthenticateRequest`, `PostAuthenticateRequest`, `AuthorizeRequest`, `PostAuthorizeRequest`, then jumps to `LogRequest`, `PostLogRequest`, `EndRequest` (status 200) and `PreSendRequestHeaders`; `ResolveRequestCache` through `PostUpdateRequestCache`, and the page, do not run. A cold hit runs all twenty-one events | `no-cache` |

Observations (dynamic):

- The section is a server-side response cache for managed output. A profile
  with a user-mode `policy` stores the second response to a URL and answers
  the following requests from it; the page or handler does not run.
- `varyByQueryString` and `varyByHeaders` key the stored copy. Without them,
  every query string shares one copy (CP18).
- `location` sets the response header and nothing else. `Server` replaces
  ASP.NET's `private` with `no-cache`; `Client` still stores on the server.
- A kernel-only profile also stores, and keeps the managed headers (CP21).
- Only a 200 is stored (CP38). Only a body under `maxResponseSize` is
  stored (CP31). A response carrying `Set-Cookie` is not stored (CP36), and
  once the application has issued a session cookie nothing new is stored for
  any page (CP37), which makes profiles inert for most Web Forms
  applications after their first session.
- CP37/CP49 is ASP.NET, not the native module, and the reference source
  shows the chain. `SessionStateModule` keeps a static `s_sessionEverSet`
  (`State/SessionStateModule.cs:189`). While it is false, the InProc
  optimization skips the session id entirely. Once any request stores a
  session it flips, and from then on every request to a page with session
  state enabled creates an id and adds the `ASP.NET_SessionId` cookie in
  `AcquireRequestState`; a page that never touches `Session` gets the cookie
  removed again in `ReleaseRequestState` (`:1397`, `RemoveSessionID`). In
  integrated mode the removal is a `Set-Cookie` header write to IIS
  (`HttpHeaderCollection.Remove` → `IIS7WorkerRequest.SetResponseHeader`),
  and `SetUnknownResponseHeader` disables both IIS caches for any response
  that names `Set-Cookie` (`Hosting/IIS7WorkerRequest.cs:2067-2071`,
  DevDiv 255268). So the wire shows no cookie, and the copy is still refused.
  A page with `EnableSessionState="False"` skips the module and keeps
  storing (CP49).
- The page cannot opt out. `SetNoStore`, `NoCache` and `Private` are stored
  anyway, and the served copy carries the profile's header, not the page's
  (CP32 to CP34). ASP.NET's own `@OutputCache` answers first and keeps its own
  header (CP35).
- The key is the request URL's path plus the named `varyBy` values; the
  extension is matched case-insensitively on the URL, so a route without an
  extension never matches (CP39 to CP44).
- A stored hit still enters the managed pipeline. Authentication and
  authorization run, then the request skips from `PostAuthorizeRequest` to
  `LogRequest` (CP27): session state, handler mapping and the handler are
  bypassed, and `EndRequest` sees status 200.
- Because authorization runs, a `<deny users="?" />` folder still turns an
  anonymous client away with the forms redirect (CP29). Because the copy is
  keyed by URL alone, any client that passes authorization gets the bytes
  rendered for whoever primed it, identity included (CP28, CP29). A 302
  refusal is not stored, but it counts toward the two-hit priming.
- The port has no equivalent: every request runs the page, and ASP.NET's own
  `OutputCache` directive is the only output cache it carries.

Port before the change that followed these readings (static-only application
on the ScenarioHost, same day): a static file answered `Cache-Control: public`
and `Expires` one day ahead, from the managed `StaticFileHandler`; its 304
answered `Cache-Control: private`. IIS's native static module sends neither
header without a profile (CP1, CP50). The port now matches that, and carries a
profile's word on every static status (ledger P58, P99).
