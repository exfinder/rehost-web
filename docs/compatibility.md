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
`FriendlyUrlsOverKestrelTests`, and `OwinCookieAuthenticationOverKestrelTests`.

| Capability | State | Boundary |
| --- | --- | --- |
| Pre-application start, C# `App_Code`, `Global.asax` | Supported | Includes `AppInitialize`, `Application_Start`, generated assembly reuse, and compile-error pages |
| `<codeSubDirectories>` and `App_GlobalResources` | Supported | Neutral resources and one culture satellite are exercised |
| C# page compilation | Supported | Roslyn provider; default language version 7.3; mapped diagnostics |
| Visual Basic | Unsupported | Configured provider fails with an actionable message |
| Custom CodeDOM providers and advanced batching | Unassessed | Provider options, batch limits, post-processors, and reproducible emission remain open |
| `.aspx` GET, `CodeFile`, common controls | Supported | Parsing, compilation, data binding, URL resolution, request validation, and warm reuse |
| Master pages and user controls | Supported | Nested masters, typed master, runtime `LoadControl`, postback, and basic fragment caching |
| Other controls, config/dynamic masters, and fragment-cache axes | Unassessed | Coverage is not inferred from related controls or runtime master selection |
| `.ashx` | Supported | Direct and Friendly URL handler compilation/execution are exercised |
| `.asmx`, `.wsdl`, `App_WebReferences` | Unsupported | General ASMX/SOAP and proxy generation are absent |
| `App_Browsers` and `.xsd` typed DataSets | Unsupported | Built-in browser definitions remain; application browser compilation and XSD build-provider generation fail explicitly |
| Precompiled deployment | Unassessed | Imported consumer path exists; neither consumer nor producer is proven cross-platform |
| `System.Web.Extensions` / ScriptManager | Partial | Rehost assembly exposes the reached full-page ScriptManager closure plus the `ListView`/`DataPager` control family, registered for `asp:` as Framework registers it; frozen template renders on macOS; the 13 release Microsoft AJAX scripts are generated from their recipes and embedded, one byte-identical to Framework's, and `ScriptResource.axd` serves them with `Sys.Res` appended; debug scripts and localized satellites are absent, so `ScriptMode.Auto` falls back to release and client strings stay invariant; async postbacks and cross-platform gates remain open |
| Page methods, `ServiceReference` proxies, profile/authentication/role application services | Unsupported | Endpoint types are WCF-hosted, so they are absent rather than deferred. `EnablePageMethods` and inline proxies fail with a named diagnostic; the application-service managers instead render client URLs to `*_JSON_AppService.axd`, which nothing serves, so those fail in the browser rather than on the server |
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
| HTTP/2 and HTTP/3 | Unassessed | No real transport gate |
| Client certificates | Unassessed | The host adapter does not expose a tested certificate contract |
| Forms, postback, view state, control state | Supported | URL-encoded and multipart parsing, event ordering, MAC enforcement, event validation, and request validation |
| Uploaded-file and raw-request `SaveAs` | Supported | Memory/disk-spill paths; Windows-rooted paths fail actionably off Windows rather than being misinterpreted |
| `Request.Filter` and raw-header grouping parity | Unassessed | No filter scenario; `HttpRequest.SaveAs` header grouping is not compared to IIS |
| Request/response cookies | Supported | Subkeys, defaults, validation, repeated `Set-Cookie`, attributes, mutation, and UTF-8 default response-header bytes |
| `Response.Headers` read/`Add`/`Set`/`Remove` off IIS | Supported | The managed collection is the response-header store: it holds what was written through it, `AppendHeader` included, and nothing else; at send, `ContentType`, the cache policy, and a set `RedirectLocation` outrank a same-named entry while `Set-Cookie` is additive, and after the first flush `Add` throws and `Remove` is inert (readings H1-H16). No IIS `Server` header exists to hold or re-emit, and the post-flush collection mirrors this host's single-shot generated block rather than the partial native block IIS showed |
| Configured non-default response-header encoding | Partial | Kestrel is pinned to Framework's UTF-8 default; `<globalization responseHeaderEncoding>` is not translated; non-ASCII request-header decoding is unassessed |
| `aspnet:MaxHttpCollectionKeys` | Supported | Opt-in: absent by default as on measured Framework 4.8.1; when configured it caps query, URL-encoded, multipart-field, and posted-file collections |
| Kestrel and `httpRuntime.maxRequestLength` limits | Supported | Each owner keeps its limit and error path; the smaller effective limit wins; legacy `system.webServer` limit translation is unassessed |
| Async pages, async module events, custom async handlers | Supported | Task-friendly synchronization context only; expected `HttpContext.Current` restoration is exercised |
| Page async timeout | Partial | TAP timeout/token behavior is exercised; a never-completing APM task remains pending, matching the measured Framework result |
| `PageAsyncTask` parallel/timeout handlers | Unsupported | Framework refuses them under the supported task-friendly context; the advertised legacy-context switch is also unsupported |
| `aspnet:UseTaskFriendlySynchronizationContext=false` | Unsupported | Activation fails naming the setting and required change |
| Illogical `CallContext` isolation | Partial | Pool-thread reuse can expose a stale illogical value; see [call-context rationale](call-context-compatibility.md) |
| Application access to remoting `CallContext` | Unsupported | Compatibility type remains internal; publishing it would claim general remoting support |
| `Response.End`, terminating redirects, `CompleteRequest` | Partial | Response/pipeline effects match; named catch/abort differences are below |
| `catch (ThreadAbortException)` around termination | Partial | Never executes because modern .NET has no compatible thread-abort signal |
| `catch (Exception)` swallowing termination | Partial | Response remains ended, but non-response side effects before the next pipeline-step boundary may run |
| `executionTimeout` | Partial | Cooperative delivery occurs between steps; an executing step is not interrupted and a never-returning step is not stopped |
| `Server.Transfer` and `Server.Execute` | Supported | Child execution, query/form semantics, writer capture, previous-page state, and static-file child are exercised |
| `Server.TransferRequest` | Unsupported | No integrated pipeline to re-enter; failure names `Transfer`/`Execute` alternatives |
| Static files through System.Web | Partial | IIS-derived extension/type map, hidden segments, normal ranges, HEAD, validators, 304, and sendfile-backed commit are exercised; `If-Range` is unassessed |
| Application `TransmitFile`/`WriteFile` with Unix-rooted paths | Partial | Framework's Windows-only physical/virtual classifier remains ambiguous; known-physical internal call sites use explicit seams |
| Friendly URLs and application routing | Partial | Extensionless pages/handlers, segments, physical redirects, route ordering, mobile selection/switching, and direct/routed authorization are exercised; route escaping breadth and real upstream rewrite variables remain open |
| Client-visible streaming, compression, WebSockets/upgrades | Unassessed | No transport contract or end-to-end gate exists |

## Configuration and IIS-derived behavior

Evidence: `IisServerConfigurationTests`, `WebServerAmendmentsOverKestrelTests`,
`HiddenSegmentsOverKestrelTests`, and `StaticFilesOverKestrelTests`.

| Capability | State | Boundary |
| --- | --- | --- |
| Framework-derived machine/root web configuration | Supported | Versioned assets default under `AppContext.BaseDirectory/configs` and may be overridden explicitly; application `web.config` is optional |
| App-root `system.webServer/staticContent` | Supported | `add`/`remove`/`clear`, MIME types, duplicate validation, and inherited baseline |
| App-root `requestFiltering/hiddenSegments` | Supported | Case-insensitive add/remove/clear; request refusal currently has an app-shaped response rather than IIS substatus 404.8 |
| Per-folder `system.webServer` | Unassessed | IIS honors it; this port currently merges application root only |
| App-root `system.webServer/defaultDocument` | Supported | Directory requests rewrite to the first existing candidate in list order (app observes the list's casing), 301 slash redirect, 403-class refusal, and `enabled="false"` narrows to directory requests alone (readings D1–D15); error bodies app-shaped pending `httpErrors` |
| Static `System.Configuration.ConfigurationManager` inside the application | Supported | `HttpConfigurationSystem` installs itself as the process configuration system at activation, as on Framework, so `AppSettings`, `ConnectionStrings`, and custom sections resolve from the site `web.config` for third-party libraries (Katana `owin:*`, Entity Framework `<connectionStrings>`/`<entityFramework>`); exercised by `OwinCookieAuthenticationOverKestrelTests` and the Identity smoke |
| Broken `system.webServer` sections | Supported | Any honored section that fails to parse or validate refuses activation, naming the file and the entry; IIS instead scoped its 500.19 to the requests reading the section, so an application it served despite a latent duplicate must be corrected before it starts here |
| Request-filtering limits, custom headers, `httpErrors` | Unassessed | Planned IIS-configuration tenants |
| `system.webServer/handlers` and `/modules` | Unassessed | Integrated registrations are not translated |
| Framework root `httpModules` defaults | Partial | `UrlAuthorization` and `UrlRoutingModule-4.0` are registered in Framework order; the remaining baseline modules await reached behavior and classification |
| Framework root handler fallbacks | Partial | Forbidden-source extensions return managed 403 and unsupported verbs return 405; omitted assembly-owned handlers fall through rather than producing an actionable unsupported diagnostic |
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
| Session state | Unassessed | Implementation is compiled but no root module is registered; `HttpContext.Session` is absent unless work explicitly enables it. A `<sessionState mode="InProc">` naming an unresolvable `customProvider` type parses and activates, as it does on Framework |
| Forms authentication, roles, profiles, anonymous identity | Unassessed | Compiled surface is not a support claim; modules/providers are not gated. `FormsAuthentication.SignOut()` from `LoginStatus` under `<authentication mode="None">` writes the same expired `.ASPXAUTH` as Framework, measured by the `WebFormsIdentityApplication` smoke, not by a standing test |
| Windows authentication and native health providers | Unassessed | Root modules are absent and no portable host translation is defined |
| URL/file authorization and impersonation | Partial | URL authorization allow/deny is exercised for direct and Friendly URL requests, including from a subdirectory `Web.config` on a case-sensitive filesystem (ledger P70); file authorization and impersonation remain unassessed |
| General cache and output cache | Unassessed | Basic fragment caching is exercised through user controls; broader cache policy/provider behavior is not |
| `WebResource.axd` | Partial | An embedded image round-trips end to end over Kestrel, byte-exact with the declared content type, and a tampered payload is refused with 404; localized satellites and `ScriptResource.axd` script delivery remain absent |
| Application Services sibling assembly | Partial | Membership/provider types and in-process loader exist; no AppDomain isolation/unload |
| ResX reading | Partial | Common strings/resources supported; legacy serialized, drawing, and platform-limited values need explicit coverage |
| SMTP configuration | Supported | Section surface, defaults, validators, and converters are tested |
| Web Services sibling assembly | Partial | Required configuration types only; ASMX/SOAP is unsupported |
| Enterprise Services/COM+ | Unsupported | Internal shape retained; transaction execution fails explicitly |
| Remote IIS configuration and Windows administration | Unsupported | Remoting/COM/IIS administration contracts are absent |

## Evidence maintenance

Kestrel integration tests are primary. The committed Framework golden remains a
frozen regression gate. New Framework work uses a captured fixture or an ad-hoc
reading when that evidence can decide the outcome; historical oracle experiments
are not recreated unless current evidence is missing or contradictory.
