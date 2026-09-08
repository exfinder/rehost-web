# IIS integrated mode: modules and handlers readings

Evidence for ledger P83, P85, P86 and the `system.webServer/handlers` and `/modules`
[compatibility rows](../compatibility.md). MH1-MH39 were observed on full IIS 10 on
`winbox`, 2026-08-22–23. Tables retain the stimulus, observable result and conclusion;
repeated setup, request transcripts and teardown are omitted.

## Method

Rounds 1–3 used an Integrated v4.0 pool, one application per case, and `MhProbes.dll`:
`LogA`/`LogB`/`LogC` recorded five pipeline events; `HandlerA`/`HandlerB` identified the
selected handler; `probe.aspx` printed `Modules.AllKeys`. Requests used on-box `curl`.
Round 4 used the equivalent `Mh2Probes.dll` rig and raw-socket requests. Unless stated,
configuration was application-level. Static controls ensured failures were not limited to
managed requests. Each app was isolated, so one invalid configuration could not mask another.

## Module collection and integrated validation

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| MH1 | Add `LogA`, then `LogB`, under `system.webServer/modules`. | Both append after inherited modules. All five events, including `EndRequest`, fire A then B. | Adds append; same-event order is effective-list order, never reversed. |
| MH2 | Remove inherited `Session`; add the same name with `LogC` type. | `LogC` fires once per event; `Session` remains second in `Modules.AllKeys`. | Replacing an inherited name changes its type but preserves its inherited position. |
| MH3 | Register identical `LogA` in classic and integrated sections; validation disabled. | One module and one event sequence. | The classic copy does not double-register. |
| MH4 | Register `LogA` only in `httpModules`; validation disabled. | No events; absent from `Modules.AllKeys`. | Classic-only modules are ignored in Integrated mode. |
| MH5 | Omit validation flag; configure `httpModules`, `httpHandlers`, or impersonation. | Respectively 500.22, 500.23, or 500.24 from `ConfigurationValidationModule` at `BeginRequest`, code `0x80070032`. The module case also rejects static files. | Default validation rejects classic Integrated-incompatible settings for the whole application. |
| MH6 | Set `validateIntegratedModeConfiguration="true"` with classic modules/handlers. | Same 500.22/500.23 shape as MH5. | Explicit `true` equals the default. |
| MH7 | Same module name maps to `LogA` in `httpModules`, `LogB` in `system.webServer/modules`; validation disabled. | Only `LogB` runs. | The integrated list is authoritative. |
| MH10 | Add `LogA` with `managedHandler`; add unconditioned `LogB`. | Page: A and B. Static file: B only, for all five events. | Unconditioned managed modules run for native static handling; `managedHandler` modules do not. |
| MH11 | Add `LogA` with valid but unsatisfied `classicMode`. | Requests succeed; `LogA` is absent and silent. | Unsatisfied valid conditions remove a module from the effective list. |
| MH12 | Add a module with condition `bogus`. | Every request: 500.0, IIS Web Core, `BeginRequest`, `0x8007000d`, “bad precondition”. | Unknown condition tokens invalidate the application. |
| MH13 | Add two modules named `Dup`. | Every request: 500.19, `0x8007000d`, duplicate collection key. | Names are unique configuration keys. |
| MH14 | Remove an absent module; add `LogA`. | Request succeeds and `LogA` runs. | Removing an absent name is tolerated. |
| MH15 | Application-level module `<clear/>`, then add `LogA`. | Every request: 500.19 lock violation, `0x80070021`. | Inherited module entries are locked; an application cannot clear them. |
| MH17 | Repeat MH10 with `runAllManagedModulesForAllRequests="true"`. | Both modules run for pages and static files. | The flag clears the effective `managedHandler` restriction. |
| MH23 | With validation flag absent, use only `httpModules` `<remove>` or `<clear/>`. | Both yield 500.22. | Any application-level classic module content triggers validation, not only adds. |
| MH24 | Put a module add in a subfolder `web.config`. | No failure, but the module never runs in or outside that folder. | Folder-level module sections are silently ignored. |
| MH33 | Put classic `httpModules` or impersonation inside `<location>`. | No validation error; the module never runs, including inside the location. Root-level control still gives 500.22. | `ConfigurationValidationModule` does not inspect classic settings inside `<location>`; they are lost silently. |
| MH34 | Put classic `httpModules` behind `configSource`. | Request gives 500.22. | Validation inspects resolved external section content. |

## Handler collection and matching

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| MH8 | Add exact and wildcard managed mappings in both orders. | First listed match wins; a leading wildcard beats a later exact mapping. Application adds beat inherited `PageHandlerFactory`. | Fresh application entries use document order, not specificity. |
| MH9 | Remove/re-add inherited `ExtensionlessUrlHandler-Integrated-4.0` before a fresh exact `api` mapping. | `/api` reaches the fresh mapping; `/nosuchthing` remaps to `StaticFile` and 404s, leaving first-pass selection ambiguous. | MH9v/MH9v2 resolve the ordering. |
| MH9v | Give the re-added inherited name marker `HandlerB`. | `/api` uses later `HandlerA`; `/nosuchthing` uses `HandlerB`. | The inherited-name entry is active but ordered after fresh application adds. |
| MH9v2 | Repeat with two fresh names: wildcard first, exact second. | Both URLs use the wildcard `HandlerB`. | Control confirms ordinary document order. |
| MH16 | Configure a handler only in classic `httpHandlers`; validation disabled. | Request falls to `StaticFile` and returns 404.0, `0x80070002`. | Classic-only handlers are ignored in Integrated mode. |
| MH18 | Clear handlers, then add one managed mapping. | Added path works; unmatched page/static paths return 404.4 at `MapRequestHandler`, no handler determined, `0x80070002`. | Handler `<clear/>` is legal and removes inherited mappings. |
| MH19 | Add two handlers with the same name. | Every request: 500.19, `0x8007000d`, duplicate collection key. | Handler names are unique configuration keys. |
| MH20 | Remove an absent handler; add a valid mapping. | Valid mapping works. | Removing an absent name is tolerated. |
| MH21 | Exact POST mapping before wildcard; GET/POST controls. | Verb mismatch skips the entry: GET falls to the next mapping or `StaticFile`; POST selects it. No mapping-layer 405. A body-bearing request without length is rejected earlier with 411. | Verb participates in first-match selection. |
| MH22 | Reference nonexistent handler and module types. | Handler: only its matched URL fails lazily with `FileNotFoundException`. Module: every request, including static, fails with managed 500 and configuration exception chain. | Handler types resolve at dispatch; module types resolve for application activation. |
| MH25 | Use valid-unsatisfied `classicMode` or invalid `bogus` on a handler. | Valid condition skips to next match. Invalid condition makes every request 500.0, IIS Web Core, `0x8007000d`. | Handler conditions follow the module validity/eligibility split. |
| MH26 | Map nonexistent `ghost.aspx`, first default resource type, then `resourceType="File"`. | Default serves the handler. `File` returns 404.0, `0x80070002`, naming the mapped handler. | `File` requires a physical file; default `Unspecified` does not. |
| MH27 | Parent and child add competing `*.aspx`; child alternatively removes parent name. | Child add wins within its subtree. Child removal reveals inherited `PageHandlerFactory` there; parent remains active at root. | Handler configuration merges per folder; deepest additions precede parent additions. |
| MH28 | Vary path/URL case, multi-segment paths, PathInfo, and verb case. | Path matching is case-insensitive; verbs are case-sensitive. `sub/marker.axd` matches any segment-boundary suffix, not root-only. `Marker.axd/extra` and `*.aspx/extra` match after PathInfo split. | Path and verb use distinct comparisons; matching operates on script path and supports suffix segments. |
| MH40 | `Global.asax` logging `Application_BeginRequest` and `Application_Error`; request a missing `.json`, `/.well-known/appspecific/com.chrome.devtools.json`, a missing `.aspx`, and a missing directory; repeat with `runAllManagedModulesForAllRequests="true"` (IIS Express 10 / 4.8, 2026-09-07). | All four answer 404. `Application_Error` fires only for the missing `.aspx` (`HttpException` 404, "The file '/nosuch.aspx' does not exist."). The static and directory misses never raise it, even when managed `BeginRequest` ran for them under RAMMFAR. The port raises it for all four: its `StaticFile` bridge refuses with a managed `HttpException(404)` at handler mapping. | A native module's 404 is a response, not a managed exception; an application's `Application_Error` never sees it. Surfaced by YAF, whose handler stores every error application-wide and shows it on `error.aspx`: with DevTools open, the Chrome probe's 404 replaced the real error. |
| MH41 | Same sites as MH40, `Global.asax` also logging `PostMapRequestHandler`, `LogRequest`, `EndRequest`, `PreSendRequestHeaders` with `Response.StatusCode`; second site with `customErrors mode="On"` and a 404 `redirect`. | Missing `.json`: `BeginRequest` 200, then `LogRequest`/`EndRequest`/`PreSendRequestHeaders` all 404, no error event; wire is IIS's own 404 page (`Content-Length` 4778, no `X-AspNet-Version`). With `customErrors` on, the `.json` stays a native 404 while `/nosuch.aspx` becomes 302 to `notfound.aspx?aspxerrorpath=/nosuch.aspx`. Missing directory: `PostMapRequestHandler` reports `TransferRequestHandler`, a child request answers the 404, and the parent's Log/End see 200. | A native miss is decided at the handler stage: everything before it ran normally, everything after it sees the 404, `customErrors` never sees it. The directory case rides `TransferRequest`, which the port does not have (P62); it is P67's, not this row's. |
| MH42 | Same `Global.asax`; request a hidden segment (`/bin/probe.dll`, `/App_Data/x.txt`, files present), a forbidden extension (`/web.config`, `/Default.aspx.cs`), with and without `runAllManagedModulesForAllRequests`; `customErrors` 404 row on the second site. | All four answer 404.8 (hidden segment) or 404.7 (extension), IIS's own page, no `Location`. With RAMMFAR: no `BeginRequest`, no `PostMapRequestHandler`, no `Application_Error`; `LogRequest`, `EndRequest`, `PreSendRequestHeaders` fire with 404. Without RAMMFAR: no managed event at all. Port, both variants: same status, same absence of `BeginRequest`, same Log/End/PreSend at 404, but `Application_Error` fires first, and the events fire without RAMMFAR too. | Request filtering ends the request ahead of `BeginRequest` and never as a managed exception; `customErrors` does not see it. Two port deltas: the error event, owned by the native-refusals design; and managed events on a native request without RAMMFAR, a `managedHandler` matter (P83), separate. |
| MH43 | Full IIS on winbox, site on 8171, requested from the Mac (remote) and from winbox (local): missing `.json`, empty directory with listing off, `POST`/`PUT` to a static file (2026-09-08). | Remote: 404, 103-byte body "The resource you are looking for has been removed, had its name changed, or is temporarily unavailable."; 403.14, 58 bytes "You do not have permission to view this directory or page."; 405, 101 bytes "The page you are looking for cannot be displayed because an invalid method (HTTP verb) is being used." with `Allow: GET, HEAD, OPTIONS, TRACE`. Local: the detailed pages, 4.8-5.3 KB. `Server: Microsoft-IIS/10.0`, `X-Powered-By: ASP.NET`, no `X-AspNet-Version` on any of them. A bodiless `POST` never reaches IIS: http.sys answers 411. | `httpErrors` default `DetailedLocalOnly`: one sentence to a remote client, the diagnostic page locally. The port's fixed page carries those sentences (P97). |
| MH44 | MH40's `Global.asax` under RAMMFAR; `POST` and `PUT` with `Content-Length: 0` to a static file, `GET` an empty directory with listing off, `GET` a `.asp` file, control `POST` to a page. | 405.0, 405.0, 403.14, 200 (classic ASP is installed on winbox and served it), 200. `Application_Error` fires for none of them; `BeginRequest` 200 and `LogRequest` with the final status for each. The directory case again shows the `TransferRequest` child (MH41). | The static handler's 405 and 403 are native responses like its 404; P97's treatment of all three matches. The classic-ASP 403 the port raises is Framework's managed `DefaultHttpHandler` answer for a box without ASP; not comparable here. |

## Request filtering

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| MH29 | Set `<fileExtensions allowUnlisted="false"/>`; then allow only `.txt`. | Empty list denies `.txt`, `.aspx`, and extensionless directory path with 404.7. Allowing `.txt` serves it; `.dat` remains 404.7. | The section becomes an allow list, including the empty extension. |
| MH30 | Application add denies `.dat`; remove inherited `.config`; clear inherited list. | Denied `.dat`: 404.7. Un-denied `.config` and `.cs`: 404.3 from missing static MIME map. `web.config`: still 404.8 from hidden segments. | Add/remove/clear merge normally; independent IIS gates still apply. |
| MH31 | Use `allowed="flase"`. | 500.19, `0x8007000d`; error names invalid Boolean attribute and file. | Boolean schema validation is strict. |
| MH32 | Deny `.dat` in subfolder `web.config`. | Subfolder file: 404.7. Root file: 404.3 from `StaticFileModule`. | Request filtering resolves per path; only the configured subtree changes. |

## Routing-time conditions

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| MH35 | `managedHandler` module plus `BeginRequest` rewrite: static URL→page, then page URL→static file. | Static-arrival request never gains the module; page-arrival request keeps it through all events. Controls select it only for the original managed URL. | Condition is decided once from the arriving URL, before `BeginRequest`; rewrite does not revisit it. |
| MH39 | For a static URL, call `RemapHandler` at `BeginRequest` to install a managed handler. | Managed handler produces the response; conditioned module remains absent after remap. Page/static controls agree with MH35. | Handler remapping also cannot change the request’s initial condition result. |

Thus every conditioned module sees one request-wide answer. A routed URL inherits the
classification of its arriving extension, not that of the eventual handler.

## Authorization, verbs and headers

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| MH36 | Deny anonymous users under classic `system.web/authorization`, then under native `system.webServer/security/authorization`. | Classic denial blocks `.aspx` with 401 but serves static `.txt`; native denial returns 401.2 for both. | Classic URL authorization is `managedHandler`-conditioned; native authorization protects all handlers. |
| MH37 | Send PUT, DELETE, OPTIONS and TRACE to static and page paths. | PUT/DELETE: 405 from `StaticFile`, `Allow: GET, HEAD, OPTIONS, TRACE`. OPTIONS: 200 from protocol handler, `Allow: OPTIONS, TRACE, GET, HEAD, POST`; application handler does not run. TRACE: 501 unless `EnableTraceMethod=1`. | Native protocol/static handlers own these unmatched verbs. `Content-Length: 0` is required to avoid earlier 411. |
| MH38 | Add custom CORS/security headers and remove inherited `X-Powered-By`; request managed, static, OPTIONS, TRACE and error responses. | Adds/removal affect all responses, including native and IIS errors. OPTIONS also sends `Public`; `X-AspNet-Version` appears only on managed output. A CORS preflight is byte-equivalent to plain OPTIONS: no allow-methods/headers or `Vary`. | `customHeaders` operates below the managed pipeline. An allow-origin header alone supports simple CORS requests, not preflight. |

## Design consequences

- Treat integrated module and handler lists as authoritative; classic registrations are
  validation input only.
- Preserve collection identity, inherited ordering tiers, per-folder handler merging, and
  first-match path/verb behavior.
- Decide `managedHandler` once from the arriving URL. Routing and remapping do not update it.
- Keep module activation eager and handler activation lazy, with actionable failures.
- Model request filtering, native authorization, protocol handlers, and custom headers as
  distinct IIS layers; managed-pipeline emulation alone cannot reproduce their reach.

The rigs were throwaway applications; no firewall changes were made. Round 4 applications
were left on `winbox` for follow-up readings when recorded.
