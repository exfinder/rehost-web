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
| Request scheme, host, and server variables | Supported | TLS/Host drive scheme, URL, name and port. All 45 Framework variables exist; IIS-only values use fixed single-site shapes or `""`. Integrated URL variables and `ServerVariables.Set` are unavailable (P76) |
| Behind a TLS-terminating proxy or load balancer | Supported | Forwarded For/Proto/Host middleware trusts loopback by default. Other proxies require `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` or `ForwardedHeadersOptions`; otherwise the app sees proxy values (P76) |
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
| Directory order and `<add assembly="*"/>` | Supported | Listings use deterministic NTFS order on every filesystem. `bin` matches case-insensitive `.dll`, ignores non-assemblies, and fails x86/identity conflicts actionably (P73) |
| C# page compilation | Supported | Roslyn provider; default language version 7.3; mapped diagnostics |
| Visual Basic | Unsupported | Configured provider fails with an actionable message |
| Custom CodeDOM providers and advanced batching | Unassessed | Provider options, batch limits, post-processors, and reproducible emission remain open |
| `.aspx` GET, `CodeFile`, common controls | Supported | Parsing, compilation, data binding, URL resolution, request validation, and warm reuse |
| Master pages and user controls | Supported | Nested masters, typed master, runtime `LoadControl`, postback, and basic fragment caching |
| Other controls, config/dynamic masters, and fragment-cache axes | Unassessed | Coverage is not inferred from related controls or runtime master selection |
| `.ashx` | Supported | Direct and Friendly URL handler compilation/execution are exercised |
| `.asmx` serving | Supported | SOAP 1.1/1.2 doc/literal invoke, complex types, headers, faults, sessions, one-way, localhost form POST, `?wsdl`, `?disco`, help page, and `[ScriptService]` JSON are exercised over Kestrel on all three OSes ([web-services-compatibility](web-services-compatibility.md)) |
| `.wsdl` build provider, `App_WebReferences`, encoded `?wsdl` | Unsupported | WSDL→proxy generation needs `System.Xml.Serialization` code-export APIs cut from modern .NET; entry points fail actionably and point at pre-generated clients |
| `Request.Browser` capabilities | Supported | Compiled Framework 4.5 factory includes all 61 definitions. `<browserCaps>` uses `HttpBrowserCapabilities`; unreachable deprecated Mobile controls are the only lost subclass distinction |
| `App_Browsers` and `.xsd` typed DataSets | Unsupported | Built-in browser definitions remain; application browser compilation and XSD build-provider generation fail explicitly |
| Precompiled deployment | Unassessed | Imported consumer path exists; neither consumer nor producer is proven cross-platform |
| `System.Web.Extensions` / ScriptManager and UpdatePanel | Supported | IIS-byte-matched async deltas, triggers, Timer, errors and redirects; 13 generated release scripts via `ScriptResource.axd`. Debug/localized resources absent: Auto falls back to invariant release. Recognized browser required ([details](extensions-compatibility.md)) |
| Page methods, `ServiceReference` proxies, profile/authentication/role application services | Partial | ASMX JSON, `/js`, `EnablePageMethods` and disabled-by-default built-in service routes work. Inline proxies compile but are unassessed; enabled provider-backed built-ins unassessed |
| QueryExtender and `QueryableDataSource` | Supported | Expression filtering and ordering over an application `QueryableDataSource` are exercised; `LinqDataSource` is excluded (`System.Data.Linq` has no modern implementation) and fails page compilation naming the missing type |
| Dynamic Data, Entity, Mobile | Unassessed | APIs outside the reached Extensions closure remain absent or unassessed. `System.Web.DynamicData` is not carried, so the Dynamic Data hook every `ItemType` data control calls is inactive rather than fatal (ledger P69) |
| Friendly URLs package/API | Partial | Modern package, original public API, routing, caching modes, redirects, authorization, generic handlers, helpers, model binding, mobile pages/masters, and view switching are exercised; advanced escaping and upstream IIS rewrite mapping remain open |
| OWIN host (`Rehost.WebForms.Owin.Host.SystemWeb`, Katana 4.2.3) | Partial | Startup discovery, module/context, cookie auth and Identity 2.2 template sign-in/out work. WebSockets, `MapOwinPath`, disconnect/shutdown and integrated stage markers unassessed; Identity smoke is manual |
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
| WebSockets (`IsWebSocketRequest`, `AcceptWebSocketRequest`, `AspNetWebSocket`) | Supported | Handshake, subprotocol, same-origin, closes and slim callback context match IIS. Post-accept body is dropped; callback `User` is null instead of IIS's anonymous principal (P80) |
| Mid-request `Response.Flush` / `FlushAsync` | Supported | Head and buffered bytes reach the client at each flush as on IIS (System.Web's own chunked framing over HTTP/1.1); late head changes, `End` after a flush, and errors after a flush behave as measured on Framework (ledger P79) |
| `TransmitFile`/`WriteFile` over 2 GB | Supported | `SupportsLongTransmitFile` is on; the native-handle overload is unsupported (ledger P78) |
| Forms, postback, view state, control state | Supported | URL-encoded and multipart parsing, event ordering, MAC enforcement, event validation, and request validation |
| Uploaded-file and raw-request `SaveAs` | Supported | Memory/disk-spill paths; Windows-rooted paths fail actionably off Windows rather than being misinterpreted |
| `Request.Filter` and raw-header grouping parity | Unassessed | No filter scenario; `HttpRequest.SaveAs` header grouping is not compared to IIS |
| Request/response cookies | Supported | Subkeys, defaults, validation, repeated `Set-Cookie`, attributes, mutation, and UTF-8 default response-header bytes |
| `Response.Headers` read/`Add`/`Set`/`Remove` off IIS | Supported | Managed writes form the store. Generated content/cache/redirect fields win; cookies append. After flush Add throws, Remove is inert. No IIS Server header; post-flush view is the host's single generated block (H1–H16) |
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
| Path-taking APIs: includes, masters, site map, data/mail/control files, response files, `MapPath` | Supported | Framework path classification and case folding, including above-root includes (P71). No portable spelling for absolute Windows physical paths; use virtual paths. Jet/OLEDB `AccessDataSource` excluded |
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
| App-root `requestFiltering/fileExtensions` | Partial | IIS deny-list and application `add`/`remove`/`clear`/`allowUnlisted` semantics are supported; denials return 404 and invalid entries fail activation. The section is root-only, while IIS resolves it per path (ledger P86, MH29-MH32) |
| URL canonicalization, path-info, `Request.RawUrl` | Supported | Adapter matches 127-request reading for slashes, encoded separators, dot segments, root-climb 403, handler path-info split and canonical path + verbatim query (P72). Two static wire-status deltas remain on IIS follow-up |
| Per-folder `system.webServer/handlers` | Supported | Each folder `Web.config` merges into an immutable effective list at activation; mapping, path-info, and `managedHandler` use the deepest configured list. Multi-level compositions follow the tested merge algorithm but are not individually measured against IIS (ledger P87, MH27) |
| Per-folder `system.webServer`, other sections | Partial | `<modules>` in a folder file is inert, which is IIS's own behavior — it ignores a subfolder section outright, silently (MH24). `staticContent`, `defaultDocument`, `requestFiltering` and the rest are still merged from the application root alone and are unassessed per folder |
| App-root `system.webServer/defaultDocument` | Supported | Directory requests rewrite to the first existing candidate in list order (app observes the list's casing), 301 slash redirect, 403-class refusal, and `enabled="false"` narrows to directory requests alone (readings D1–D15); error bodies app-shaped pending `httpErrors` |
| Static `System.Configuration.ConfigurationManager` inside the application | Supported | Activation installs site `web.config` as process configuration, so app settings, connections and custom sections work for libraries such as Katana/EF; integration-tested |
| Broken `system.webServer` sections | Supported | Any honored section that fails to parse or validate refuses activation, naming the file and the entry; IIS instead scoped its 500.19 to the requests reading the section, so an application it served despite a latent duplicate must be corrected before it starts here |
| Request-filtering limits, custom headers, `httpErrors` | Unassessed | Planned IIS-configuration tenants; the [IIS integration plan](follow-ups/iis-integration-plan.md) holds them |
| `system.webServer/handlers` and `/modules` | Supported | Integrated merged lists replace classic sections: ordering, mutations, conditions, lazy handlers, native bridges and folder handlers. Boundaries: verb statuses, TransferRequest, some global/pre-send conditioning, and earlier activation failures ([MH readings](research/iis-modules-handlers-readings.md)) |
| Shipped baseline `<modules>` and `<handlers>` | Supported | `applicationHost.config` carries measured managed modules/order and handler rows; classic defaults are retired. Native bridge makes StaticFile/protocol rows composable. Windows/FileAuthorization registrations are inert; registration is not a feature claim (P84/P85) |
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
| Session state | Partial | InProc/Custom locking and concurrency work. StateServer unsupported; SQLServer unimplemented; expiry `Session_End`, cookieless identity unassessed; impersonated `useHostingIdentity` degenerates ([evidence](follow-ups/session-state.md)) |
| Forms authentication, roles, profiles, anonymous identity | Partial | Fake-provider denial/sign-in/cookies/profile/output-cache work. Real SQL-provider journey, general claims payloads and full `LoginStatus` sign-out remain open ([evidence](follow-ups/forms-authentication.md)) |
| Windows authentication and native health providers | Unsupported | No portable Windows-login translation; registered Windows/FileAuthorization modules stay inert and health modules are absent. Explicit `<authentication mode="Windows">` fails activation; the untouched section default does not |
| URL/file authorization and impersonation | Partial | Managed URL authorization works for direct/routed/child config but, as on IIS, does not protect native static files (MH36). Native `system.webServer` authorization, file authorization and impersonation are unimplemented; native section is currently ignored ([backlog](backlog.md)) |
| General cache and output cache | Partial | Page output cache and basic fragment cache work. Static files opt out to preserve IIS revalidation semantics (P58). Providers, broader `VaryBy*` and policy remain unassessed |
| `WebResource.axd` | Partial | An embedded image round-trips end to end over Kestrel, byte-exact with the declared content type, and a tampered payload is refused with 404; localized satellites remain absent. `ScriptResource.axd` support is recorded with `System.Web.Extensions` above |
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
