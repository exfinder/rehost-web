# Compatibility feature map

Status: open. Priority: high. Depends on explicit support decisions.

## Goal

Maintain a precise user-facing map of source/API and behavioral compatibility.
For each feature record: supported, partially supported, unsupported, or
unassessed; platforms; security/trust constraints; failure mode; and owning
contract/test.

## Rules

- “Supported” means portable tested behavior.
- Nothing is supported only on Windows.
- Partial support names the exact boundary.
- Unsupported behavior fails explicitly where reachable.
- Unassessed is not equivalent to unsupported or supported.
- Story completion updates the map; history stays in Git.
- A story that adds a section retires the rows it supersedes. Four stale rows
  found in one day — cookies, `.master`/`.ascx`, the `Response.End` length
  residue, and Transfer/Execute — were each left behind by a story that added
  its own section and outvoted an older row instead of deleting it. A
  superseded row reads as a current claim.

Implemented behavior remains canonical in feature contracts until this aggregate
map is complete.

## Supported applications

| Feature | State | Boundary |
| --- | --- | --- |
| `<httpRuntime targetFramework="4.5" />` or later | Required | Every request renders the refusal naming the fix, which is how Framework reports a startup configuration failure. Below 4.5 the quirks switch selects pre-4.5 behavior at some thirty sites, including `machineKey compatibilityMode`, which routes view state through native cryptography this port refuses. The Visual Studio project template has emitted the attribute since 4.5 |
| Native ASP.NET libraries | Unsupported | `UnsafeNativeMethods` refuses on type initialization, so an unported path names the contract rather than reporting a missing `webengine4.dll` (ledger P41) |
| Legacy machine key cryptography | Unsupported | Reached by `machineKey compatibilityMode` below `Framework45`, which the gate above already refuses, and by the obsolete `MachineKey.Encode`/`Decode` on any application. `MachineKey.Protect`/`Unprotect` are unaffected |
| `BinaryFormatter` serialization | Supported | .NET 9 removed the implementation, so the port carries the out-of-band package and turns the runtime switch on for consuming applications. Reached from view state, out-of-process session state, the roles cookie, out-of-process output cache, preserved compilation results, `LosFormatter`, and binary-serialized `.resx` nodes. Deserialization exposure equals .NET Framework's — neither narrowed nor widened (ledger P42, P43) |

## Compilation substrate

Supported means portable tested behavior on macOS `arm64` and Windows `x64`,
covered by `CodegenSubstrateTests`.

| Feature | State | Boundary |
| --- | --- | --- |
| `[PreApplicationStartMethod]` in `bin` assemblies | Supported | Runs before any generation, once per process |
| `App_Code` (C#) | Supported | Includes the static `AppInitialize` entry point |
| `<codeSubDirectories>` | Supported | Each named directory compiles into its own assembly first; the main assembly may reference it. Framework's mixed-language motivation does not apply, since Visual Basic is unsupported |
| `App_GlobalResources` | Supported | Neutral resx plus culture satellites, reached through the generated strongly typed class |
| `Global.asax` | Supported | Inline `<script runat="server">`; `Application_Start` and request events |
| Reuse across restart | Supported | An unchanged application restarts without recompiling; an edited one recompiles |
| Two processes, one codegen segment | Supported | Serialized by the cross-process compilation mutex |
| Compile error in top-level code | Supported | Activation completes; every request renders the compilation error page with diagnostics, as Framework does. Recovery is a process restart, since file-change notification is disabled |
| `App_WebReferences`, `.wsdl` | Unsupported | Fails naming the limitation |
| `App_Browsers` | Unsupported | Fails naming the limitation. The built-in browser definitions still apply; only application-level `.browser` overrides are refused |
| Visual Basic | Unsupported | Registered provider fails naming the limitation and the fix |
| `.master`, `.ascx` | Supported | Their build providers compile on first request and `PartialCachingControl` reaches the cache substrate; covered by `MasterPagesOverKestrelTests` rather than `CodegenSubstrateTests`, with the composition boundaries under [master pages and user controls](#master-pages-and-user-controls) |
| `.ashx`, `.asmx` | Unassessed | Providers registered, no slice compiles them yet. `.ashx` is mapped to `SimpleHandlerFactory` by the shipped configuration but nothing exercises it |

## Pages

Supported means portable tested behavior on macOS `arm64` and Windows `x64`,
covered by `PageCompilationTests` and `PageOverKestrelTests`.

| Feature | State | Boundary |
| --- | --- | --- |
| `.aspx` GET, parse, compile, render | Supported | Routed by the shipped `httpHandlers` mapping to `PageHandlerFactory`; compiled on first request and reused until the markup changes |
| `CodeFile` code-behind | Supported | The `.aspx.cs` compiles alongside the generated page class as a partial |
| `Label`, `Repeater`, `HyperLink`, `Image`, `Panel` | Supported | Includes `ItemTemplate` compiled to its own builder, `RepeaterItem` naming containers, data binding from code-behind, and `~/` URL resolution |
| Literal markup of 256 characters or more | Supported | Emitted as a metadata string rather than a Win32 resource; rendered bytes unchanged, per-request transcode instead of a byte copy (ledger P39) |
| Request validation | Supported | Framework's default; a query value containing `<` is refused before the page runs |
| `<form runat="server">`, postback, view state | Supported | Covered under request bodies below; `__VIEWSTATEGENERATOR` carries a permanent divergence recorded in [machine key and view state](machine-key-and-viewstate-bootstrap.md) |
| Controls requiring a server form | Partially assessed | `TextBox`, `Button`, `LinkButton`, and `FileUpload` render and post back. Other controls calling `VerifyRenderingInServerForm`, `GridView` among them, are unassessed rather than blocked |
| Static files via `StaticFileHandler` | Partially supported | An extension the application maps to `System.Web.StaticFileHandler` in `httpHandlers` serves with its `MimeMapping` content type; a missing file is a 404 (ledger P54). The shipped configuration maps no content extension — Framework left static files to IIS, so an application opts each extension in. Range, `If-Modified-Since`/`If-Range`, and ETag revalidation are unassessed |

### Master pages and user controls

No port seam exists here: composition rides untouched Reference Source over the
already-ported parser, codegen, and control-tree substrate, and worked on first
probe. Pinned by `MasterPagesOverKestrelTests` because the `.master`/`.ascx`
build providers and `PartialCachingControl` reach the port-owned compilation
and cache substrate for the first time.

| Feature | State | Boundary |
| --- | --- | --- |
| Master page composition | Supported | `MasterPageFile`, `asp:Content`/`ContentPlaceHolder` (including placeholder default content), page `Title` override, `head runat="server"` |
| Nested master pages | Supported | Two levels verified |
| `@ MasterType` | Supported | The typed `Master` property compiles and reaches master members |
| User controls | Supported | `@ Register Src` and runtime `LoadControl`; Framework's naming-container ID mangling (`MainContent_…`, `ctl00$…`) is pinned literally |
| Postback through a master-hosted form | Supported | Event routing and event validation under the mangled names |
| Fragment caching | Supported | `@ OutputCache` on a control replays the fragment while the page re-renders. Only `Duration` + `VaryByParam="none"` assessed; other `VaryBy*` axes, `Shared`, and `CachePolicy` are unassessed |
| Config-level and dynamic masters | Unassessed | `<pages masterPageFile>` and `PreInit` assignment are untested rather than blocked |

### Server.Transfer and Server.Execute

Untouched imported code over the P52 termination unwind; every probed shape
worked unchanged. Pinned by `ServerTransferOverKestrelTests`.

| Feature | State | Boundary |
| --- | --- | --- |
| `Server.Transfer` | Supported | Child renders in place, parent tail suppressed through the End unwind, parent output written before the call survives (Framework's buffered-write behavior); query preserved, overridden by a `?` in the path, or cleared with `preserveForm: false`; `PreviousPage` set; the compiled-handler overload matches |
| `Server.Execute` | Supported | Inline composition with the parent continuing, capture into a `TextWriter`, `preserveForm` semantics as Transfer; always runs the child through the `IHttpAsyncHandler` arm |
| `Server.Execute` to a static file | Supported | The mapped-handler arm writes the file into the response through the P61 known-physical seam |
| `Application_Error` → `GetLastError` → `Transfer` | Supported | The error page renders with `HttpUnhandledException` wrapping the page's exception, Framework's wrapper |
| `Server.TransferRequest` | Unsupported | No pipeline to re-enter on this host. Refused on every platform with `PlatformNotSupportedException` naming `Server.Transfer`/`Server.Execute` as replacements (ledger P62); implementing integrated-style re-entry is an owned follow-up |

## Request bodies

| Feature | State | Boundary |
| --- | --- | --- |
| Raw request bodies | Supported | Real Kestrel HTTP/1.1 fixed-length and delayed chunked bodies pass on macOS `arm64` and Windows `x64` through `InputStream`, `BinaryRead`, buffered input, bufferless sync/APM, and enabled async preload. The Framework deterministic matrix, `Expect: 100-continue`, mid-read abort, and unread-body drain are covered. HTTP/2 and HTTP/3 are slice 6 gates |
| Forms, postback, view state, multipart uploads | Supported | Urlencoded and multipart form parsing, view state, control state, event ordering, MAC enforcement, and request validation, covered by `PostbackOverKestrelTests` and `MultipartPostbackOverKestrelTests`. `__VIEWSTATEGENERATOR` carries a permanent divergence (ledger P48) |
| Saving uploaded content to disk | Supported | `HttpPostedFile.SaveAs` and `HttpRequest.SaveAs`, from memory and from the temp file used above `requestLengthDiskThreshold`. `requireRootedSaveAsPath` keeps Framework's default and message. A path rooted only on Windows is refused off Windows, naming the platform rather than reporting "not rooted" or silently writing a file named for the whole path (ledger P50); Windows behavior is unchanged. Whether both hosts classify every header identically is unverified and reaches only the raw-request save |
| `Request.Filter` | Unassessed | Nothing installs an input filter yet |
| Request collection key limit | Supported, opt-in | Off by default, matching .NET Framework 4.8.1 measured at `Int32.MaxValue`, with the key absent from every shipped configuration file. An application carrying it in its own `web.config` is read identically by both runtimes. `aspnet:MaxHttpCollectionKeys` caps query string, urlencoded and multipart form fields, and posted files; the check runs before each add, so the configured value is the last accepted count. A urlencoded body reports the refusal as `HttpException` — "The URL-encoded form data is not valid" — carrying the real `InvalidOperationException` as its inner exception, which is Framework's own wrapping. ASP.NET Core's own 1024-value form limit does not apply: System.Web parses the body, not the host |
| Request-body size limits | Supported | Kestrel `MaxRequestBodySize` remains host-owned and `httpRuntime.maxRequestLength` remains System.Web-owned; the smaller effective limit wins. The adapter changes neither and rejects no mismatch. Kestrel rejection remains host-owned; System.Web rejection remains pipeline-owned. Known and unknown lengths pass on both supported platforms. Legacy `system.webServer` `maxAllowedContentLength` mapping belongs to its dedicated follow-up |

## Cookies

Supported means portable tested behavior on macOS `arm64` and Windows `x64`,
covered by `CookiesOverKestrelTests`. Story:
[cookies](cookies.md).

| Feature | State | Boundary |
| --- | --- | --- |
| `Request.Cookies` | Supported | The adapter hands System.Web the whole `Cookie` header and System.Web parses it: sub-key values, valueless cookies, repeated names, and `$Path`/`$Domain` attributes on the preceding cookie. Repeated `Cookie` header lines are joined with `", "`, the HTTP rule IIS also applies, which makes them one malformed cookie on both; RFC 6265 forbids a client from sending them. Request validation refuses a dangerous cookie value before the handler runs |
| `Response.Cookies` | Supported | Each cookie leaves as its own `Set-Cookie` line with Framework's own attribute text, including the pre-RFC `expires=Www, dd-Mmm-yyyy HH:mm:ss GMT` form and `HttpOnly` via `Request.Browser`. `Set` replaces and `Remove` drops the line entirely, because the classic pipeline generates headers once; deleting a cookie in a browser still needs an expired one. Response cookies also appear in the same request's `Request.Cookies` |
| Cookie defaults from `<httpCookies>` | Supported | The section is absent from the shipped root configuration, as it is from every configuration file on a .NET Framework 4.8.1 machine, so both runtimes take the section defaults. Cookies parsed from the request take those defaults too, which is 4.8.1 behavior and postdates the pinned Reference Source; `aspnet:EnsureCookieDefaults="false"` restores the older shape on either runtime |
| Non-ASCII response header values | Unsupported | A cookie value — or any header value — outside ASCII fails the request with an empty 500 from Kestrel's header validation. Framework writes header bytes in `Response.HeaderEncoding`, UTF-8 by default. Open, with the options recorded in [cookies](cookies.md) |
| Session, forms authentication, roles, and anonymous identification cookies | Unassessed | Owned by those subsystems, along with every cookieless mode |

## Request termination

Supported means portable tested behavior on macOS `arm64` and Windows `x64`,
covered by `ResponseEndOverKestrelTests`. Story:
[Response.End and request termination](response-end-and-termination-plan.md);
mechanism and residual gaps are recorded there (ledger P51, P52).

| Feature | State | Boundary |
| --- | --- | --- |
| `Response.End` | Supported | Completes the response, then unwinds on an internal control-flow exception the pipeline recovers: code after `End()` does not run, `finally` blocks and `Page_Unload` run, bytes written before are sent and bytes after are discarded, `PostRequestHandlerExecute`/`ReleaseRequestState`/`UpdateRequestCache` are skipped, `EndRequest` runs, `Application_Error` does not fire, `Server.GetLastError()` stays null. The body seals at the call, but headers — cookies included — stay amendable through `EndRequest` and leave with the final flush, as Framework's abort arm delivered (readings R19–R24, ledger P55); an application's own `Flush()` still seals them, as it did there too. The ended response and the terminating redirect both state Framework's exact `Content-Length` and no `Transfer-Encoding`, asserted on the raw socket by `HeaderAmendmentOverKestrelTests`; only an application's own `Flush()` forfeits the length, as it did on Framework |
| `Response.Redirect(url)` | Supported | 302, `Location`, Framework's exact "Object moved" body replacing earlier buffered content, then terminates as `End` does. `Redirect(url, false)` returns and later writes append (reading R6); `RedirectToRoute*` never terminates (`endResponse` hardcoded false); redirect after a flush throws naming the cause (R8). Those three are untouched imported source over exercised substrate |
| `HttpApplication.CompleteRequest` | Supported | No unwind: the calling code finishes, subsequent stages are skipped, `EndRequest` runs. Gated by the committed parity golden |
| `catch (ThreadAbortException)` in application code | Partial | Compiles and never executes: termination no longer travels as `ThreadAbortException`. An application that reacts to termination by that name silently loses the reaction |
| `catch (Exception)` swallowing termination | Partial | The catch observes an internal `CancelModuleException`. It cannot produce response output and cannot un-complete the request, but statements between the catch and the end of the current pipeline step still run, and their side effects outside the response are visible — on Framework the automatic re-raise prevented them. `Thread.ResetAbort` has no counterpart: an application that cancels termination to resume the page is explicitly broken |
| `executionTimeout` | Partial | Enforced cooperatively (ledger P53): the scan cancels `Request.TimedOutToken` and flags the request at budget, and the timeout is delivered **between pipeline steps** as Framework's error — `HttpException` *"Request timed out."*, `Application_Error`, 500. The boundary: a step in progress is never interrupted, so the 500 arrives when the current step returns — a single slow call inside a handler completes before the client sees the timeout, where Framework's abort usually landed mid-call — and a step that never returns is never stopped. `ThreadAbortOnTimeout = false` suppresses delivery while the token still cancels; `debug="true"` and an attached debugger suppress the sweep as on Framework. Scan period is 15 s as Framework's, configurable via the port-owned `rehost:RequestTimeoutScanSeconds` appSettings key (clamped to ≥ 1). Connection abort at budget — bounding the client's wait for a blocked step — is an owned follow-up in [request termination and timeouts](request-termination-and-timeouts.md). Async waits are never flagged: a request pending in async completes past budget exactly as Framework (measured 17 s against a 1 s budget on 4.8.1), pinned by `TimeoutOverKestrelTests` |
| `Server.Transfer`, `Server.Execute` | Supported | Both ride this termination unwind; the behavior rows are under [Server.Transfer and Server.Execute](#servertransfer-and-serverexecute) (ledger P61, P62). Framework readings R13–R15 and the child-`End` trap are recorded in the termination story |

## Async pages and pipeline

Supported means portable tested behavior on macOS `arm64` and Windows `x64`,
covered by `AsyncPagesOverKestrelTests` and `AsyncPipelineOverKestrelTests`;
per-stage context expectations are win-oracle 4.8.1 readings (ledger P63, P64).

| Feature | State | Boundary |
| --- | --- | --- |
| `Async="true"` + `RegisterAsyncTask` (`Func<Task>`, `Func<CancellationToken, Task>`) | Supported | Truly-pending resumes marshal through `AspNetSynchronizationContext` with `HttpContext.Current` restored; `ConfigureAwait(false)` continuations see it null, as Framework guaranteed. The token is cancellable and untripped within `AsyncTimeout` |
| `async void` page event handlers | Supported | Operation counting holds the response until the handler's continuations finish; same restore semantics |
| APM `PageAsyncTask` (begin/end), `AddOnPreRenderCompleteAsync` | Supported | Sequential execution in registration order; `End` and render run with the context restored |
| `PageAsyncTask` `timeoutHandler` / `executeInParallel` | Unsupported | Framework itself refuses both under the task-friendly context (measured 500 naming the switch), and the switch it advertises — the legacy context — is refused at preflight, so the refusal is total here rather than configuration-dependent |
| `AsyncTimeout` | Partial | A TAP task past the budget fails with Framework's *"An asynchronous operation exceeded the page timeout."* 500, and the `CancellationToken` overload's token fires at budget — both measured. The boundary, also measured: an APM task that never completes is never timed out — Framework hangs identically (75 s+ against a 1 s budget), and `executionTimeout` does not rescue either runtime because a pending-async request is never aborted |
| `aspnet:UseTaskFriendlySynchronizationContext=false` | Unsupported | Activation preflight refuses the legacy synchronization context naming the setting and the change (ledger P64) |
| Async module events (`EventHandlerTaskAsyncHelper`, `AddOn*Async`) | Supported | The awaited event resumes with the context restored before the pipeline continues |
| Custom `IHttpAsyncHandler`, truly-pending completion | Supported | Completion crosses threads to the host commit; `EndProcessRequest` runs **without** `HttpContext.Current` — Framework leaves it null there too (measured) — so handlers must use the context they were handed |
| `CallContext` public API from application code | Unsupported | The compat `CallContext` stays internal by decision ([CallContext compatibility](../call-context-compatibility.md)): publishing it would imply general remoting compatibility. Application calls fail at compile time |

## Shipped root configuration

The portable root web configuration is derived from the pinned .NET Framework
4.8.1 baseline. Two collections omit entries whose types this port does not
carry. A build provider resolves its type only when a file of that extension is
compiled, so a registered extension states nothing about whether the slice that
compiles it exists yet.

| Section | Omitted | Reason | Effect |
| --- | --- | --- | --- |
| `compilation/buildProviders` | `.edmx`, `.xoml`, `.svc`, `.xamlx` | Types live in `System.Data.Entity.Design`, `System.WorkflowServices`, `System.ServiceModel.Activation`, and `System.Xaml.Hosting` | Unsupported. Files of these types are ignored rather than reported against a Framework assembly that will never exist here |
| `pages/namespaces` | `System.Web.DynamicData` | Assembly absent | Generated code does not import it. Registering it would fail every compilation, not only code that uses it |
| `pages/controls` | `System.Web.UI`, `...WebControls`, `...WebControls.Expressions`, `System.Web.DynamicData`, `...WebControls` | Types live in `System.Web.Extensions`, `System.Web.DynamicData`, and `System.Web.Entity` | Unsupported. Five of Framework's six entries. The surviving entry registers `System.Web.UI.WebControls.WebParts`; `WebControls` itself needs no entry because `[assembly:TagPrefix]` already registers the `asp:` prefix for it |
| `httpHandlers` | `*_AppService.axd`, `ScriptResource.axd`, `*.asmx`, `*.rem`, `*.soap`, `*.svc`, `*.xoml`, `*.xamlx` | Types live in `System.Web.Extensions`, `System.Runtime.Remoting`, `System.ServiceModel.Activation`, and `System.Xaml.Hosting` | Unsupported. Requests for these extensions fall through to the trailing catch-all instead of being reported against an assembly that will never exist here. Framework marks all eight `validate="false"`, so it too defers them to request time |
| `browserCaps/result` | `System.Web.Mobile.MobileCapabilities` | Type lives in `System.Web.Mobile` | **Substituted**, not omitted, with its own base `System.Web.HttpBrowserCapabilities`. Omitting the element defaults the result to `HttpCapabilitiesBase`, which `HttpRequest.Browser` cannot cast; omitting the whole section leaves `Request.Browser` null and every control reading it throws. Code casting to `MobileCapabilities` cannot exist here because the assembly is absent. `System.Web.Mobile` is otherwise unported |

### Behavior the handler list brings with it

Framework's list is taken whole apart from the rows above, so its trailing
entries come too, and they are not inert.

| Path | Effect here |
| --- | --- |
| `*` → `DefaultHttpHandler` | `HttpWorkerRequest.SupportsExecuteUrl` is false for every worker request in this port, so `DefaultHttpHandler` does not delegate to a host and serves the file itself through `StaticFileHandler`. System.Web therefore serves static files, alongside whatever the ASP.NET Core host serves |
| `*` → `HttpMethodNotAllowedHandler` | Verbs outside `GET,HEAD,POST` get Framework's 405 rather than a 404 |
| `*.cs`, `*.config`, `*.asax`, `*.master`, and the rest of the forbidden set | 403 rather than the 404 these paths returned before the list was shipped. Asserted by `PageOverKestrelTests` |

`.wsdl` and `.xsd` stay registered under the type names Framework used, which
resolve here to the port's explicit refusal and its compatibility provider.

## Done when

The first runnable request and every encountered IIS/Windows feature have
entries suitable for a future README compatibility section.
