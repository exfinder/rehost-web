# Compatibility

This is the sole current support map. Detail documents explain mechanisms and
rationale but do not widen these claims.

## Reading the map

- **Supported**: behavior-focused tests pass on Windows x64, Linux, and
  macOS arm64.
- **Partial**: the exact supported boundary and divergence are stated.
- **Unsupported**: deliberately excluded; reached entry points fail explicitly.
- **Unassessed**: present or plausible, but carries no support claim.

Evidence is named once per section. A row cites separate evidence only for a
partial result, divergence, or surprising boundary.

## Application and deployment model

Evidence: `ApplicationBootstrapTests`, `ApplicationConfigurationPublicationTests`,
`CodegenSubstrateTests`, and the parity gates.

| Capability | State | Boundary |
| --- | --- | --- |
| Classic managed request pipeline over Kestrel | Supported | Enters `HttpRuntime.ProcessRequest(HttpWorkerRequest)`; IIS native notification scheduling is not used |
| One application per process/current AppDomain | Supported | Reinitialization and in-process replacement are unavailable; restart means process replacement |
| Request scheme, host, and server variables | Supported | `IsSecureConnection`, `Request.Url`, `SERVER_NAME`/`SERVER_PORT` come from TLS and the Host header as on IIS; all 45 Framework server variables are present, IIS-native ones with fixed single-site shapes (`APPL_MD_PATH=/LM/W3SVC/1/ROOT`, `SERVER_SOFTWARE=Kestrel`), certificate/TLS-strength ones `""`; `UNENCODED_URL`/`HTTP_URL`-class integrated-mode variables and `ServerVariables.Set` are unavailable (ledger P76) |
| Behind a TLS-terminating proxy or load balancer | Supported | `UseRehostWebForms` registers ASP.NET Core's forwarded-headers middleware (`X-Forwarded-For/Proto/Host`); trust is the framework default — loopback proxies only — so a container behind a proxy sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (not set by the official `mcr.microsoft.com/dotnet/aspnet` images) or configures `ForwardedHeadersOptions`; without that, forwarded values are ignored and the app sees the proxy's scheme and address (ledger P76) |
| Host registration and activation lifecycle | Partial | Static one-shot bootstrap mutates current-AppDomain state before listen; first-request activation is lazy and shared; bootstrap failure is terminal |
| Runtime-originated shutdown/restart notification | Unsupported | Only host-initiated shutdown is wired; runtime restart requests do not stop the ASP.NET Core host |
| Full trust | Supported | Partial trust and CAS policy are unsupported |
| `<httpRuntime targetFramework="4.5" />` or later | Required | Lower values select unassessed quirks and native crypto; startup fails naming the fix |
| Web Site runtime-compilation model | Partial | Exercised by fixtures and the sample; consumer build/publish packaging remains incomplete |
| Web Application Project model | Supported | Both Visual Studio templates build as frozen WAP trees through the `RehostAppContentRoot`/`RehostSiteContentRoot` sidecar contract from packages, and their `smoke.sh` journeys pass on all three platforms; publish payload and designer-file policy remain open in the backlog |
| Microsoft `System.Web` binary identity | Unsupported | Source/API compatibility after rebuild is the target; strong-name/binary interchangeability is not promised |
| Native ASP.NET/IIS libraries | Unsupported | `UnsafeNativeMethods` fails explicitly when an unported path reaches it |

## Compilation and pages

Evidence: `CodegenSubstrateTests`, `PageCompilationTests`,
`PageOverKestrelTests`, `MasterPagesOverKestrelTests`,
`FriendlyUrlsOverKestrelTests`, `OwinCookieAuthenticationOverKestrelTests`,
and `AjaxOverKestrelTests`.

| Capability | State | Boundary |
| --- | --- | --- |
| Pre-application start, C# `App_Code`, `Global.asax` | Supported | Includes `AppInitialize`, `Application_Start`, generated assembly reuse, and compile-error pages |
| `<codeSubDirectories>` and `App_GlobalResources` | Supported | Neutral resources and one culture satellite are exercised |
| Directory order and `<add assembly="*"/>` | Supported | Every listing is in NTFS order on every filesystem, so duplicate-type blame, theme `<link>` order, and codegen reuse are deterministic (not Framework's exact batch sequence); the `bin` scan matches `.DLL` on Linux and ignores non-assembly files as Framework did, while an x86-only or identity-duplicated assembly still fails activation as on Framework (ledger P73) |
| C# page compilation | Supported | Roslyn provider; default language version 7.3; mapped diagnostics |
| Visual Basic | Unsupported | Configured provider fails with an actionable message |
| Custom CodeDOM providers and advanced batching | Unassessed | Provider options, batch limits, post-processors, and reproducible emission remain open |
| `.aspx` GET, `CodeFile`, common controls | Supported | Parsing, compilation, data binding, URL resolution, request validation, and warm reuse |
| Master pages and user controls | Supported | Nested masters, typed master, runtime `LoadControl`, postback, and basic fragment caching |
| Other controls, config/dynamic masters, and fragment-cache axes | Unassessed | Coverage is not inferred from related controls or runtime master selection |
| `.ashx` | Supported | Direct and Friendly URL handler compilation/execution are exercised |
| `.asmx` serving | Supported | SOAP 1.1/1.2 doc/literal invoke, complex types, headers, faults, sessions, one-way, localhost form POST, `?wsdl`, `?disco`, help page, and `[ScriptService]` JSON are exercised over Kestrel on all three OSes ([web-services-compatibility](web-services-compatibility.md)) |
| `.wsdl` build provider, `App_WebReferences`, encoded `?wsdl` | Unsupported | WSDL→proxy generation needs `System.Xml.Serialization` code-export APIs cut from modern .NET; entry points fail actionably and point at pre-generated clients |
| `Request.Browser` capabilities | Supported | The compiled-in `BrowserCapabilitiesFactory` carries Framework 4.5's 61 definitions, mobile browsers included (BlackBerry, Opera Mini, Opera Mobile); none were dropped. The shipped `<browserCaps>` names `HttpBrowserCapabilities` where Framework named `MobileCapabilities` from `System.Web.Mobile`: that subclass exists for the mobile controls deprecated in ASP.NET 4.0, which no ported path reaches. The element cannot be omitted, because `HttpRequest.Browser` cannot cast the result that leaves behind |
| `App_Browsers` and `.xsd` typed DataSets | Unsupported | Built-in browser definitions remain; application browser compilation and XSD build-provider generation fail explicitly |
| Precompiled deployment | Unassessed | Imported consumer path exists; neither consumer nor producer is proven cross-platform |
| `System.Web.Extensions` / ScriptManager and UpdatePanel | Supported | Async postbacks over `ScriptModule-4.0`: UpdatePanel deltas, outside triggers, Timer ticks, the error token and `AsyncPostBackErrorMessage`, and redirect-to-`pageRedirect` interception, byte-matched to full IIS 4.8 on the wire; the 13 release Microsoft AJAX scripts are generated from their recipes and served through `ScriptResource.axd` with `Sys.Res` appended; debug scripts and localized satellites are absent, so `ScriptMode.Auto` falls back to release and client strings stay invariant; partial rendering needs a recognized browser, as on Framework ([extensions-compatibility](extensions-compatibility.md)) |
| Page methods, `ServiceReference` proxies, profile/authentication/role application services | Partial | `[ScriptService]` `.asmx` JSON calls, `/js` proxies, and `EnablePageMethods` (the `PageMethods` proxy and the JSON `{"d":…}` answer on the page path) are exercised; inline `ServiceReference` proxies compile but are unassessed. The built-in `*_JSON_AppService.axd` names map to the real internal script services and refuse with Framework's disabled-service error until enabled; enabling them over membership/role/profile providers is unassessed |
| QueryExtender and `QueryableDataSource` | Supported | Expression filtering and ordering over an application `QueryableDataSource` are exercised; `LinqDataSource` is excluded (`System.Data.Linq` has no modern implementation) and fails page compilation naming the missing type |
| Dynamic Data, Entity, Mobile | Unassessed | APIs outside the reached Extensions closure remain absent or unassessed. `System.Web.DynamicData` is not carried, so the Dynamic Data hook every `ItemType` data control calls is inactive rather than fatal (ledger P69) |
| Friendly URLs package/API | Partial | Modern package, original public API, routing, caching modes, redirects, authorization, generic handlers, helpers, model binding, mobile pages/masters, and view switching are exercised; advanced escaping and upstream IIS rewrite mapping remain open |
| OWIN host (`Rehost.WebForms.Owin.Host.SystemWeb`, Katana 4.2.3) | Partial | `OwinStartup` discovery incl. `owin:AppStartup` from `web.config`, the pre-start-registered module, `HttpContext.GetOwinContext()`, and Katana cookie authentication (challenge → 302 to `LoginPath`, sign-in cookie, sign-out) are exercised over the classic pipeline with the `MachineKey` protector, and the Visual Studio Identity template app registers, signs in, and signs out end to end over OWIN cookie authentication and ASP.NET Identity 2.2 (`apps/WebFormsIdentityApplication/smoke.sh`, a manual run rather than a standing test); WebSockets, `MapOwinPath` routes, disconnect and shutdown detection, and stage markers for integrated-only events (`MapRequestHandler`) are unassessed |
| Optimization/WebForms and WebGrease | Partial | Full source compiles; frozen-template debug bundle expansion runs on macOS; production combination/minification breadth and cross-platform gates remain open |

## Requests, pages, and responses

Evidence: Hosting scenario tests named `RequestBody*`, `Postback*`,
`MultipartPostback*`, `UploadSave*`, `RawRequestSave*`, `Cookies*`,
`AsyncPages*`, `AsyncPipeline*`, `ResponseEnd*`, `HeaderAmendment*`,
`ResponseHeaders*`, `ServerTransfer*`, `StaticFiles*`, and `FriendlyUrls*`.

| Capability | State | Boundary |
| --- | --- | --- |
| HTTP/1.1 request bodies | Supported | Fixed-length/chunked, sync/APM reads, buffered/bufferless input, async preload, abort, drain, and independent host/System.Web size limits |
| HTTP/2 | Supported | The request-body surfaces run over cleartext HTTP/2 (`Http2OverKestrelTests`); System.Web sees its usual HTTP/1.1-shaped request (ledger P78) |
| HTTP/3 | Unassessed | No transport gate |
| Client certificates | Unsupported | `Request.ClientCertificate` reports not present; behind a TLS-terminating proxy Kestrel never sees one, and forwarded-certificate translation into `CERT_*` is the recorded design when a consumer needs it (ledger P78) |
| WebSockets (`IsWebSocketRequest`, `AcceptWebSocketRequest`, `AspNetWebSocket`) | Supported | Over ASP.NET Core's WebSocket middleware, registered by `UseRehostWebForms`; handshake headers, sub-protocol negotiation, `RequireSameOrigin`, both close directions, and Framework's slimmed callback context as measured on IIS; body written after the accept is dropped (IIS sent it raw); `User` is null in the callback where IIS carried the anonymous principal across the transition (ledger P80) |
| Mid-request `Response.Flush` / `FlushAsync` | Supported | Head and buffered bytes reach the client at each flush as on IIS (System.Web's own chunked framing over HTTP/1.1); late head changes, `End` after a flush, and errors after a flush behave as measured on Framework (ledger P79) |
| `TransmitFile`/`WriteFile` over 2 GB | Supported | `SupportsLongTransmitFile` is on; the native-handle overload is unsupported (ledger P78) |
| Forms, postback, view state, control state | Supported | URL-encoded and multipart parsing, event ordering, MAC enforcement, event validation, and request validation |
| Uploaded-file and raw-request `SaveAs` | Supported | Memory/disk-spill paths; Windows-rooted paths fail actionably off Windows rather than being misinterpreted |
| `Request.Filter` and raw-header grouping parity | Unassessed | No filter scenario; `HttpRequest.SaveAs` header grouping is not compared to IIS |
| Request/response cookies | Supported | Subkeys, defaults, validation, repeated `Set-Cookie`, attributes, mutation, and UTF-8 default response-header bytes |
| `Response.Headers` read/`Add`/`Set`/`Remove` off IIS | Supported | The managed collection is the response-header store: it holds what was written through it, `AppendHeader` included, and nothing else; at send, `ContentType`, the cache policy, and a set `RedirectLocation` outrank a same-named entry while `Set-Cookie` is additive, and after the first flush `Add` throws and `Remove` is inert (readings H1-H16). No IIS `Server` header exists to hold or re-emit, and the post-flush collection mirrors this host's single-shot generated block rather than the partial native block IIS showed |
| Request- and response-header byte encodings | Supported | Request-header bytes decode as IIS did (UTF-8, Latin-1 fallback, never 400); response headers leave in `<globalization responseHeaderEncoding>` (UTF-8 default; `iso-8859-1` exercised), read once the application has activated (ledger P77) |
| `aspnet:MaxHttpCollectionKeys` | Supported | Opt-in: absent by default as on measured Framework 4.8.1; when configured it caps query, URL-encoded, multipart-field, and posted-file collections |
| Kestrel and `httpRuntime.maxRequestLength` limits | Supported | Each owner keeps its limit and error path; the smaller effective limit wins; legacy `system.webServer` limit translation is unassessed |
| Async pages, async module events, custom async handlers | Supported | Task-friendly synchronization context only; expected `HttpContext.Current` restoration is exercised |
| Page async timeout | Partial | TAP timeout/token behavior is exercised; a never-completing APM task remains pending, matching the measured Framework result |
| `PageAsyncTask` parallel/timeout handlers | Unsupported | Framework refuses them under the supported task-friendly context; the advertised legacy-context switch is also unsupported |
| `aspnet:UseTaskFriendlySynchronizationContext=false` | Unsupported | Activation fails naming the setting and required change |
| Illogical `CallContext` isolation | Supported with a boundary | Framework-exact for every System.Web path; a same-scope capture landing on its origin thread while data is still set is visible, unreachable through System.Web; net481-vs-port contract suite; see [call-context rationale](call-context-compatibility.md) |
| Application access to remoting `CallContext` | Unsupported | Compatibility type remains internal; publishing it would claim general remoting support |
| `Response.End`, terminating redirects, `CompleteRequest` | Partial | Response/pipeline effects match; named catch/abort differences are below |
| `catch (ThreadAbortException)` around termination | Partial | Never executes because modern .NET has no compatible thread-abort signal |
| `catch (Exception)` swallowing termination | Partial | Response remains ended, but non-response side effects before the next pipeline-step boundary may run |
| `executionTimeout` | Partial | Cooperative delivery occurs between steps; an executing step is not interrupted and a never-returning step is not stopped |
| `Server.Transfer` and `Server.Execute` | Supported | Child execution, query/form semantics, writer capture, previous-page state, and static-file child are exercised |
| `Server.TransferRequest` | Unsupported | No integrated pipeline to re-enter; failure names `Transfer`/`Execute` alternatives |
| Static files through System.Web | Partial | IIS-derived extension/type map, hidden segments, normal ranges, HEAD, validators, 304, and sendfile-backed commit are exercised; `If-Range` is unassessed |
| Path-taking APIs: server includes, `<pages masterPageFile>`, XML site map, `XmlDataSource`/`MailDefinition`/`Control.OpenFile`, `WriteFile`/`TransmitFile`, `Server.MapPath` | Supported | Strings classify as on Framework (`/`-rooted and relative are virtual, climbing above the root is refused, `//server/share` is UNC-physical) and wrong casing folds on case-sensitive filesystems, including a server include composed above the application root (ledger P71); an absolute physical path has no Unix spelling in these APIs — use a virtual path. `AccessDataSource` (Jet/OLEDB) is Windows-only and out of scope |
| Friendly URLs and application routing | Partial | Extensionless pages/handlers, segments, physical redirects, route ordering, mobile selection/switching, and direct/routed authorization are exercised; route escaping breadth and real upstream rewrite variables remain open |
| Response compression, HTTP/3 | Unassessed | Compression is IIS's own module ([IIS-role follow-up](follow-ups/iis-role-behaviors.md)); HTTP/3 has no transport gate. Client-visible streaming and WebSockets are now Supported (rows above, ledger P79/P80) |

## Configuration and IIS-derived behavior

Evidence: `IisServerConfigurationTests`, `IisRegistrationsTests`,
`IisPreConditionsTests`, `IntegratedModulesTests`, `IntegratedHandlersTests`,
`ShippedRegistrationBaselineTests`, `ClassicSectionValidationTests`,
`WebServerAmendmentsOverKestrelTests`, `ModulesOverKestrelTests`,
`HandlersOverKestrelTests`, `MigratedAppOverKestrelTests`,
`ClassicSectionRefusalOverKestrelTests`, `HiddenSegmentsOverKestrelTests`, and
`StaticFilesOverKestrelTests`.

| Capability | State | Boundary |
| --- | --- | --- |
| Framework-derived machine/root web configuration | Supported | Versioned assets default under `AppContext.BaseDirectory/configs` and may be overridden explicitly; application `web.config` is optional |
| App-root `system.webServer/staticContent` | Supported | `add`/`remove`/`clear`, MIME types, duplicate validation, and inherited baseline |
| App-root `requestFiltering/hiddenSegments` | Supported | Case-insensitive add/remove/clear; request refusal currently has an app-shaped response rather than IIS substatus 404.8 |
| App-root `requestFiltering/fileExtensions` | Supported | The golden deny list is transcribed into the shipped baseline and amended by the same collection semantics; a denied extension is refused on the script path alone, as IIS judged it (ledger P86). Divergence from the port's own past: these URLs answer 404, the status IIS gave, where the retired classic `<httpHandlers>` table answered 403. The two lists never coincided — `.mdb` and `.java` carry `staticContent` MIME types and only the deny list stops them — and `web.config` now answers 404 from the hidden-segment rule as IIS did |
| URL canonicalization, path-info, `Request.RawUrl` | Supported | http.sys-equivalent canonicalization at the adapter (`\\`, `%2F`, repeated separators, dot segments; a climb above the root is 403 at the front door), IIS's handler-mapping `FilePath`/`PathInfo` split, and `RawUrl` as the canonical decoded path plus verbatim query; every handler-visible value in the 127-request IIS reading matches (ledger P72). Wire-only leftovers (invalid-character static names 400 vs 404, static trailing separator 404 vs 500.0) are on the IIS-role follow-up; denied extensions now answer 404 from the request-filtering row above |
| Per-folder `system.webServer/handlers` | Supported | Resolved per directory: every folder `web.config` below the application root is read and merged at activation, one level per folder, and a request is answered from the list built for its own directory (ledger P87, MH27). The deepest add wins for a pattern two levels claim, a folder `remove` of a parent's add takes effect in that folder alone and matching falls back to the inherited row there, and a folder-scoped forbidden mapping therefore blocks requests into the folder and nothing outside it. The same list decides the `SCRIPT_NAME`/`PATH_INFO` split and the per-request `managedHandler` condition. Compositions MH27 does not pin — a third level, a folder `remove` and re-add of the parent's name, a folder `clear`, a folder re-adding an inherited name, and the waiver a folder inherits — follow from applying the measured per-level algorithm level by level and are named as unmeasured in `IisFolderHandlersTests` |
| Per-folder `system.webServer`, other sections | Partial | `<modules>` in a folder file is inert, which is IIS's own behavior — it ignores a subfolder section outright, silently (MH24). `staticContent`, `defaultDocument`, `requestFiltering` and the rest are still merged from the application root alone and are unassessed per folder |
| App-root `system.webServer/defaultDocument` | Supported | Directory requests rewrite to the first existing candidate in list order (app observes the list's casing), 301 slash redirect, 403-class refusal, and `enabled="false"` narrows to directory requests alone (readings D1–D15); error bodies app-shaped pending `httpErrors` |
| Static `System.Configuration.ConfigurationManager` inside the application | Supported | `HttpConfigurationSystem` installs itself as the process configuration system at activation, as on Framework, so `AppSettings`, `ConnectionStrings`, and custom sections resolve from the site `web.config` for third-party libraries (Katana `owin:*`, Entity Framework `<connectionStrings>`/`<entityFramework>`); exercised by `OwinCookieAuthenticationOverKestrelTests` and the Identity smoke |
| Broken `system.webServer` sections | Supported | Any honored section that fails to parse or validate refuses activation, naming the file and the entry; IIS instead scoped its 500.19 to the requests reading the section, so an application it served despite a latent duplicate must be corrected before it starts here |
| Request-filtering limits, custom headers, `httpErrors` | Unassessed | Planned IIS-configuration tenants; the [IIS integration plan](follow-ups/iis-integration-plan.md) holds them |
| `system.webServer/handlers` and `/modules` | Supported | The port models an integrated application pool permanently: the merged baseline-plus-application list is the registration authority for both collections, and the classic `system.web` sections are dead text under `validateIntegratedModeConfiguration="false"`, exactly as they were on IIS (ledger P83/P85, [readings MH1-MH28](research/iis-modules-handlers-readings.md)). Merge semantics are the measured ones: a re-added inherited name keeps its inherited position (MH2, MH9v), fresh module adds append at the tail and fire in list order with the end-side events not reversed (MH1), fresh handler adds are consulted ahead of inherited mappings while a re-added inherited name is not (MH8, MH9v), handler selection is first match in document order with a case-insensitive path and a case-sensitive verb list (MH8, MH28), a verb mismatch falls through instead of answering 405 (MH21), `resourceType="File"` 404s naming the entry (MH26), an absent `remove` is tolerated (MH14, MH20), `clear` is honored for handlers and refused for modules as IIS's lock violation (MH15, MH18), `preCondition` tokens are evaluated against a fixed integrated 64-bit v4.0 identity with `runAllManagedModulesForAllRequests` clearing `managedHandler` collection-wide (MH10, MH11, MH17, MH25), and a handler type resolves lazily at first match so a broken row fails only its own URLs (MH22a). Strictness deltas, all in the same direction — a startup refusal where IIS failed every request to the application including static files: app-level classic registration content or `identity impersonate="true"` without the flag (MH5, MH6, MH23), a duplicate `add` name (MH13, MH19), an unknown `preCondition` token (MH12, MH25), a `modules=` attribute naming a native module the port has no bridge for, an out-of-vocabulary `resourceType`, a module `type` that will not load (MH22b, refused when the pipeline is first built), classic registration content in a folder `web.config`, judged by the same rule as the application root's with the waiver inherited from above (unmeasured: MH5 measured the root), and two folder `web.config` files whose directories differ only by case, which are one configuration path and which only a case-sensitive filesystem can hold. Named boundaries: `OPTIONS` and `TRACE` answer 405 from the port's own protocol handler rather than IIS's 200 with an `Allow` header, so `OPTIONS` on an `.aspx` no longer runs the page; `resourceType="Either"`/`"Directory"` carry their documented meaning, not a measured one; the `ExtensionlessUrlHandler-Integrated-4.0` row is transparent to dispatch because `Server.TransferRequest` itself stays deferred; `global.asax` event handlers run unconditioned where integrated IIS gave them `managedHandler`; and `PreSendRequestHeaders`/`PreSendRequestContent` are one multicast delegate, so a conditioned module's handlers for them are not skipped. Per-folder `<handlers>` is the row above |
| Shipped baseline `<modules>` and `<handlers>` | Supported | The shipped `applicationHost.config` transcribes the IIS golden's full managed module set in its measured order (MH1) and its handler rows, and the classic `<httpModules>`/`<httpHandlers>` defaults are retired; nothing on this runtime reads them. `WindowsAuthentication` and `FileAuthorization` register faithfully and stay inert (ledger P84), and the `StaticFile` trio plus `ProtocolSupportModule` resolve through a fixed native bridge so an application's `remove`/re-map of them composes through ordinary list semantics (ledger P85). Registration is not a feature claim: what the authentication, roles, profile and anonymous-identity modules support is the rows in the section below |
| Unhonored `system.webServer` sections | Unassessed | Currently tolerated/ignored; no behavior claim follows |
| Configuration reload | Partial | Configuration is immutable for the generation, but file changes are not watched or diagnosed; operators must replace the process explicitly |

## State, security, and ancillary assemblies

Evidence: `ViewStateSerializationTests`, `MixedFarmOverKestrelTests`,
`BinaryFormatterEnablementTests`, `ResXResourceReaderTests`, configuration tests,
and explicit unsupported-contract tests.

| Capability | State | Boundary |
| --- | --- | --- |
| View-state protection with explicit literal `<machineKey>` | Supported | Framework-to-port captured postback passes for one root page; reverse direction has an on-demand Framework reading; broader cross-runtime page/type derivation is unassessed |
| `__VIEWSTATEGENERATOR` across Framework and port | Partial | Stable values differ; valid payload interchange works, but corrupt cross-runtime payloads can take a different error branch |
| Auto-generated machine keys | Partial | Random process-scoped keys work but do not survive restart or scale out; a diagnostic names the consequence |
| `,IsolateApps` / `,IsolateByAppId` key suffixes | Unassessed | Hash derivation differs across runtimes; mixed farms must use bare literal keys |
| Legacy `MachineKey.Encode`/`Decode` crypto | Unsupported | Requires refused native/pre-4.5 behavior; `Protect`/`Unprotect` are unaffected |
| `BinaryFormatter` compatibility | Supported | Trusted-input legacy surface only; out-of-band implementation and runtime switch are supplied |
| Session state | Partial | `InProc` and `Custom` are delivered: `Session` is registered by the shipped IIS baseline `<modules>`, values round-trip under the `ASP.NET_SessionId` cookie, requests sharing a session serialize while separate sessions and read-only acquires do not, and a configured `SessionStateStoreProviderBase` is driven `GetItemExclusive`/`SetAndReleaseItemExclusive`. Activation refuses `StateServer` (unsupported: the client is native) and `SQLServer` (not yet implemented). `Session_End` is proven only on the `Session.Abandon` path — expiry depends on the cache sweep thread and is unassessed. Cookieless identity is unassessed: `UseUri` has no structural blocker, while `AutoDetect`/`UseDeviceProfile` additionally rest on the substituted browser capabilities. `useHostingIdentity` is degenerate because impersonation is inert (ledger P07). Framework readings behind these claims are in [session state](follow-ups/session-state.md) |
| Forms authentication, roles, profiles, anonymous identity | Partial | The five modules are registered by the shipped IIS baseline `<modules>` and the journey is covered against fake providers by `FormsAuthenticationOverKestrelTests`, matching readings R-FA1 to R-FA9: a denied request redirects with the anonymous cookie, `SetAuthCookie` authenticates the next request, the role cookie is written on the first role check and answers later requests without a second store fetch, and an anonymous profile value survives across requests. `SqlMembershipProvider`, `SqlProfileProvider`, and `SqlRoleProvider` carry Framework's machine.config defaults but are unproven against a real database; no `apps/` sample completes register/sign-in/role-check/sign-out against SQL Server yet. `RolePrincipal` round-trips through the role cookie only, not through a general `BinaryFormatter` payload such as session state (ledger P81/P82). `FormsAuthentication.SignOut()` from `LoginStatus` under `<authentication mode="None">` writes the same expired `.ASPXAUTH` as Framework, measured by the `WebFormsIdentityApplication` smoke, not by a standing test |
| Windows authentication and native health providers | Unsupported | No portable host supplies a Windows login, and no host translation is defined. `WindowsAuthenticationModule` and `FileAuthorizationModule` are registered from the golden baseline and inert (ledger P84); the health-monitoring modules are absent. `<authentication mode="Windows">` is refused at activation naming the consequence, every request would stay anonymous and authorization rules would not apply; the section default of `Windows` is not treated as a request for it, so only an application that wrote the mode itself is refused |
| URL/file authorization and impersonation | Partial | URL authorization allow/deny is exercised for direct and Friendly URL requests, including from a subdirectory `Web.config` on a case-sensitive filesystem (ledger P70); file authorization and impersonation remain unassessed |
| General cache and output cache | Partial | `OutputCacheModule` is registered by the shipped IIS baseline `<modules>`, and a page declaring `<%@ OutputCache %>` is served from the store on the second request with matching `Cache-Control`, `Expires`, and body (reading R-FA9). Static files opt out, because IIS served those from its native module before any managed module could store one (ledger P58). Basic fragment caching is exercised through user controls; cache providers, `VaryBy*` beyond `none`, and broader policy are unassessed |
| `WebResource.axd` | Partial | An embedded image round-trips end to end over Kestrel, byte-exact with the declared content type, and a tampered payload is refused with 404; localized satellites and `ScriptResource.axd` script delivery remain absent |
| Application Services sibling assembly | Partial | Membership/provider types and in-process loader exist; no AppDomain isolation/unload |
| ResX reading | Partial | Common strings/resources supported; legacy serialized, drawing, and platform-limited values need explicit coverage |
| SMTP configuration | Supported | Section surface, defaults, validators, and converters are tested |
| Web Services sibling assembly | Partial | Full reference-source import minus WSDL→proxy generation, the VS debugger COM channel, and ADSI discovery; server and client runtimes, WS-I checking, and the full `<webServices>` section ship ([web-services-compatibility](web-services-compatibility.md)) |
| Enterprise Services/COM+ | Unsupported | Internal shape retained; transaction execution fails explicitly |
| Remote IIS configuration and Windows administration | Unsupported | Remoting/COM/IIS administration contracts are absent |

## Evidence maintenance

Kestrel integration tests are primary. The committed Framework golden remains a
frozen regression gate. New Framework work uses a captured fixture or an ad-hoc
reading when that evidence can decide the outcome; historical oracle experiments
are not recreated unless current evidence is missing or contradictory.
