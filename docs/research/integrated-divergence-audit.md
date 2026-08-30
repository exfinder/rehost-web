# Managed integrated-vs-classic divergence audit

The port runs the classic managed pipeline ([ADR 0001](../adr/0001-runtime-compatibility-model.md)),
but the migrating audience ran IIS integrated mode: every integrated-mode branch in the
imported source is behavior they observed and this runtime never executes. This audit
enumerates those branches so each one ends as take-integrated-branch, shim, or a fail-fast
diagnostic naming the boundary — never a silent classic-mode difference
([backlog](../backlog.md)).

Research only. Current support lives in the [compatibility map](../compatibility.md);
priority lives in the backlog. Readings IV1-IV16 below were taken after the first pass and
have narrowed several entries; rows carry the reading id that settles them.

## Method

`HttpRuntime.UseIntegratedPipeline` is `_useIntegratedPipeline`, written only by
`PopulateIISVersionInformation` under `IsEngineLoaded`, which is permanently false in the
port (ledger P04). Every `UseIntegratedPipeline` site therefore takes its classic arm,
unconditionally, and every `_wr is IIS7WorkerRequest` test is false — `AspNetCoreWorkerRequest`
is not an `IIS7WorkerRequest`. Sites were read in context, not classified from the grep line;
line numbers drift, so members are named. Coverage:

- all 75 `UseIntegratedPipeline` sites across 19 files (grep-complete, none sampled);
- the integrated-only public API surface reached by `SR.Requires_Iis_Integrated_Mode`,
  `SR.Requires_Iis_7`, `SR.Requires_Iis_75_Integrated`,
  `SR.Method_Not_Supported_By_Iis_Integrated_Mode`, and `IIS7WorkerRequest` type tests
  (second table set below).

Where a classification or disposition needed the integrated observable, it was measured
rather than inferred: [readings IV1-IV16](#framework-readings) run one application under an
integrated and a classic application pool on the same IIS 10, so the only variable is
pipeline mode and the classic leg is what the port reproduces today.

Classification:

- **PLUMBING** — not application-visible: native buffer/notification handoffs, perf
  counters, trace/ETW glue, debug assertions.
- **RESOLVED** — application-visible and already handled; the fence, ledger id, ADR or
  compatibility row is cited.
- **APP-VISIBLE OPEN** — application-visible and unresolved.

## Counts

| Classification | `UseIntegratedPipeline` sites | Other integrated gates (members) |
| --- | --- | --- |
| PLUMBING | 32 | 17 |
| RESOLVED | 10 | 10 |
| APP-VISIBLE OPEN | 33 | 8 |
| Total | 75 | 35 |

The 33 open `UseIntegratedPipeline` sites collapse to 16 distinct behaviors: the
`MapRequestHandler`/`LogRequest`/`PostLogRequest` event family alone accounts for 12 of them,
the `HideRequestResponse` pairs for 4, and the `HostingEnvironment` throttle accessors for
another 4. One first-pass open item — the `Response.Redirect` content type — closed as
PLUMBING once measured (IV13); one new open member was found by measurement (IV12).

## `HttpApplication.cs` — 34 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `IsContainerInitalizationAllowed` | True while re-running `InitInternal` after IIS event registration | PLUMBING | Module step containers exist only for native notification dispatch |
| `ThrowIfEventBindingDisallowed` | `InvalidOperationException` "Event handlers can only be bound to HttpApplication events during IHttpModule initialization" (IV10) | **OPEN** | Measured: classic accepts the binding silently and the handler never runs, because `CreateEventExecutionSteps` snapshotted handlers at init (IV10) |
| `FindISessionStateModule` | Returns the session module so `EnsureReleaseState` can release before a child request | PLUMBING | Child `Server.Execute` shares the parent's session; the release exists for `TransferRequest`, which is unsupported (P62) |
| `AcquireNotifcationContextLock` / `ReleaseNotifcationContextLock` (2 sites) | `Debug.Assert` | PLUMBING | Debug-only; callers are integrated-only |
| `AsyncResult` get/set (2 sites) | Stores the pending result on `NotificationContext` instead of `_ar` | PLUMBING | Per-notification storage; classic uses the field |
| `MapRequestHandler` event add/remove (2 sites) | Fires after IIS has already mapped the handler, immediately before `PostMapRequestHandler`, with identical observable state (IV1, IV4) | **OPEN** | `PlatformNotSupportedException`; classic `BuildSteps` has no such step |
| `LogRequest` event add/remove (2 sites) | Fires after `PostUpdateRequestCache` and **before** `EndRequest` (IV1) | **OPEN** | Same |
| `PostLogRequest` event add/remove (2 sites) | Fires between `LogRequest` and `EndRequest` (IV1) | **OPEN** | Same |
| `AddOnMapRequestHandlerAsync` (2 overloads) | Async form of the above | **OPEN** | Same |
| `AddOnLogRequestAsync` (2 overloads) | Async form | **OPEN** | Same |
| `AddOnPostLogRequestAsync` (2 overloads) | Async form | **OPEN** | Same |
| `ProcessSpecialRequest` enter/exit (2 sites) | Sets `HttpContext.HideRequestResponse`, so `Context.Request`/`Response` throw inside `Application_Start` and friends | **OPEN** | Port leaves them reachable — more permissive than what the audience observed |
| `InitInternal` module build | `InitIntegratedModules` from the merged `system.webServer/modules` list | RESOLVED | Port's `InitModules` is fenced to build the same integrated collection (P83) |
| `InitInternal` `HideRequestResponse` around `Init()` (2 sites) | Request/response hidden during `Init()` and module init | **OPEN** | Same family as `ProcessSpecialRequest` |
| `InitInternal` step manager | `PipelineStepManager` | RESOLVED | Classic `ApplicationStepManager` is the decision ([ADR 0001](../adr/0001-runtime-compatibility-model.md)) |
| `DisposeInternal` module key | Tracks `_currentModuleCollectionKey` so a module can unregister during `Dispose` | PLUMBING | Event maps exist only in integrated |
| `HookupEventHandlersForApplicationAndModules` | A failing `add_XXX` rethrows; classic swallows it | **OPEN** | Measured on a real classic pool (IV2): `Application_MapRequestHandler`, `Application_LogRequest` and `Application_PostLogRequest` are dropped with no error, no 500 and no log entry — the application starts and serves normally without them |
| `OnExecuteRequestStep` | Registers a wrapper around every pipeline step | **OPEN** | `PlatformNotSupportedException`; `ExecuteStepImpl` already honours `_stepInvoker` in both managers, so only the gate blocks it |
| `AssignContext` | `Debug.Assert` | PLUMBING | |
| `AsyncAppEventHandlersTable.AddHandler` | Also adds an `AsyncEventExecutionStep` to the module's event map | PLUMBING | Event maps are the native dispatch table |
| `AsyncEventExecutionStep` ctor chaining | Passes the mode to an overload that ignores the parameter | PLUMBING | Parameter unused in both arms |
| `CallHandlerExecutionStep.Execute` | `IIS7WorkerRequest.IsHandlerExecutionDenied()` → 403 with `PageForbiddenErrorFormatter` | **OPEN** | Native handler-execution denial has no managed equivalent; related to MH36 |
| `CallFilterExecutionStep.Execute` | Disables the `LogRequest` notification after `UpdateRequestCache` | PLUMBING | Notification suppression |
| `StepManager.CompleteRequest` | Marks `NotificationContext.RequestCompleted` | PLUMBING | |

## `HttpContext.cs` — 5 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `SetSkipAuthorizationNoDemand` | Persists into the `IS_LOGIN_PAGE` server variable so IIS skips its own authorization | RESOLVED | Native `system.webServer` authorization is unimplemented and the section ignored; managed `SkipAuthorization` is unaffected (compatibility: URL/file authorization) |
| `CurrentNotification` get/set (2 sites) | Returns the executing `RequestNotification`; the full managed-event-to-notification map is measured in IV6 | **OPEN** | `PlatformNotSupportedException` on classic (IV6) |
| `IsPostNotification` get/set (2 sites) | Post-phase flag; every `Post*` managed event reports `true` on its base notification (IV6) | **OPEN** | `PlatformNotSupportedException` on classic (IV6) |

## `HttpRuntime.cs` — 4 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `UsingIntegratedPipeline` (public) | `true` under an integrated pool; `false` under a classic pool on the same IIS 10, which also reports `HttpRuntime.IISVersion` as 8.0 rather than 10.0 (IV16) | **OPEN** | The port models an integrated pool for modules (P83), handlers (P85) and request filtering (P86) but answers `false` here, so third-party feature detection silently takes classic branches |
| `UseIntegratedPipeline` (internal getter) | Definition site | PLUMBING | |
| `Dispose` drain | `PipelineRuntime.WaitForRequestsToDrain()` vs active-count spin | PLUMBING | Process replacement owns shutdown ([ADR 0012](../adr/0012-runtime-initiated-restart.md)) |
| `ProcessRequest(HttpWorkerRequest)` | Refuses the classic entry point | RESOLVED | Inverse gate; this entry point is the port's engine ([ADR 0001](../adr/0001-runtime-compatibility-model.md)) |

## `HttpResponse.cs` — 2 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `Redirect` (VSO 360276) | Sets `ContentType = "text/html"` before writing the Object-moved body | PLUMBING | Measured byte-identical 302 responses from integrated and classic pools on the same IIS 10 (IV13): the classic default already emits `Content-Type: text/html; charset=utf-8` |
| `PushPromise` | Calls `IIS7WorkerRequest.PushPromise` | **OPEN** | The imported `catch (PlatformNotSupportedException)` swallows the refusal, so the call is a silent no-op |

## `HttpWriter.cs` — 6 sites

`HttpResponseUnmanagedBufferElement` static ctor, instance ctor, finalizer and `ForceRecycle`
choose the IIS integrated buffer pool over `UnsafeNativeMethods.BufferPoolGetPool`;
`HttpSubstBlockResponseElement.PointerEquals` and `HttpWriter.DisposeIntegratedBuffers`
are `Debug.Assert`s. All **PLUMBING**: `CreateNewMemoryBufferElement` is fenced to
`HttpResponseManagedBufferElement`, so the unmanaged element is never constructed.

## `httpserverutility.cs` — 1 site

`HttpServerUtility.TransferRequest` — **RESOLVED**. Ledger P62 and the compatibility row:
`PlatformNotSupportedException` naming the `Server.Transfer`/`Execute` alternatives, in place
of Framework's "switch IIS modes" message. The refusal is already fenced with a comment.

## `DefaultHttpHandler.cs` — 1 site

`BeginProcessRequest` — **OPEN** (very low). Integrated refuses the handler outright; the port
takes the classic arm, where `CanExecuteUrlForEntireResponse` is false (`SupportsExecuteUrl`
defaults to false) and the request degrades into the port-owned static-file path (P58). An
application that ran on integrated cannot have depended on this handler.

## `UI/TraceContext.cs` — 1 site

`AddRequestData` re-inserts the entity body via `Request.InsertEntityBody` so a native handler
can still read it when tracing is on. **PLUMBING** — no native handler follows.

## `Cache/OutputCache.cs` — 2 sites

`DependencyRemovedCallback` and `EntryRemovedCallback` flush the HTTP.sys kernel cache through
`UnsafeIISMethods.MgdFlushKernelCache` or `UnsafeNativeMethods.InvalidateKernelCache`. Both
**PLUMBING**: the classic arm is reachable only when `KernelCacheEntryKey`/`_kernelCacheUrl`
is non-null, which only IIS kernel caching sets, so the unported native call is never made.

## `State/SessionStateModule.cs` — 4 sites

`InitModuleFromConfig` (InProc, StateServer) sets `s_canSkipEndRequestCall`;
`OnReleaseState` disables the `EndRequest` notification; `ReleaseSessionState` releases early
for child requests. All **PLUMBING** — notification suppression is a native-dispatch
optimization, and the early release pairs with `FindISessionStateModule` above.

## `Security/*` — 7 sites

| Site | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `UrlAuthorizationModule.IsEnabled` | Reads `HttpApplication.IntegratedModuleList` | RESOLVED | Already fenced to the merged list; the classic `<httpModules>` read is dead (P83) |
| `FileAuthorizationModule.CheckFileAccessForUser` | `s_Enabled = true` unconditionally | RESOLVED | Module is inert (P84). Note the inconsistency: unlike `UrlAuthorizationModule` this classic arm still reads the retired `<httpModules>` section, so the public API silently reports access granted |
| `WindowsAuthenticationModule.OnEnter` | Takes the principal IIS already set; classic reads `LOGON_USER`/`AUTH_TYPE` | RESOLVED | Registered but inert; `mode="Windows"` fails activation (P84) |
| `RoleManagerModule.OnEnter` (2 sites) | `DisableNotifications(EndRequest)` when roles are off or uncached | PLUMBING | Perf only |
| `DefaultAuthenticationModule.Authenticate` event add | Refuses the subscription | **OPEN** (low) | Inverse gate: the port accepts an event the audience could not use |
| `DefaultAuthenticationModule.Init` | Hooks `PostAuthenticateRequest`; classic hooks `DefaultAuthentication` | **OPEN** | Measured (IV15): on integrated `Context.User` is null through `AuthenticateRequest` and set by `PostAuthenticateRequest`; on classic it is already set when `AuthenticateRequest` runs |

## `Hosting/HostingEnvironment.cs` — 4 sites

`MaxConcurrentRequestsPerCPU` and `MaxConcurrentThreadsPerCPU`, get and set — **OPEN** (low).
`PlatformNotSupportedException` today; the values are IIS pool throttles with no portable
equivalent (Kestrel's limits belong to the host).

## `Hosting/SimpleWorkerRequest.cs` — 2 sites

`UpdateResponseCounters`/`UpdateRequestCounters` skip perf counters when integrated used a fake
worker request to initialize. **PLUMBING** — counters are inert (P04).

## `IisTraceListener.cs`, `Management/IisTraceWebEventProvider.cs` — 2 sites

Both constructors refuse unless IIS 7+. **RESOLVED**: `RaiseTraceEvent` and `EtwTrace` are inert
by construction and the native health providers are unsupported
([diagnostics alternatives](portable-request-diagnostics-alternatives.md), compatibility:
Windows authentication and native health providers). The message names IIS 7 rather than the
port boundary — wording, not behavior.

## Integrated gates reached by other means

`IIS7WorkerRequest` type tests and `SR.Requires_Iis_*` guards, outside the
`UseIntegratedPipeline` set.

| Site | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `HttpRequest.EnsureHeaders` | Collection is writable: `Set`/`Add`/`Remove` succeed, `Add` appends comma-joined, `Clear` throws `NotSupportedException` (IV7); mutations feed `ServerVariables` and `Request.Url` with the caveats in IV8 | **OPEN** | Port calls `MakeReadOnly()` (measured `IsReadOnly=True` on a classic pool, `False` on integrated — IV9), so `Request.Headers.Set/Add/Remove` throws where the audience mutated request headers freely |
| `HttpRequest.GetServerVars` | Collection is writable | RESOLVED | `ServerVariables.Set` unavailable, stated in P76 and the compatibility row |
| `HttpResponse.Headers` | Native header block backs the collection | RESOLVED | P68, readings H1–H16; fenced so the managed collection is the store |
| `HttpResponse.SubStatusCode` get/set | IIS substatus for the error code | **OPEN** | `PlatformNotSupportedException`; the port has no substatus channel (hidden-segment 404.8 is already noted as app-shaped) |
| `HttpResponse.AddOnSendingHeaders` | Per-request pre-send callback; measured firing inside the `SendResponse` notification with the response still 200 and headers not yet emitted (IV11) | **OPEN** | `PlatformNotSupportedException` on classic (IV11). `OwinCallContext.RegisterForOnSendingHeaders` probes it reflectively and swallows the failure, so Katana loses its non-OWIN flush notification |
| `HttpResponse.ClientDisconnectedToken` | Cancellation token signalled on client disconnect (IIS 7.5+) | **OPEN** | `PlatformNotSupportedException`; long-poll/SignalR-shaped code uses it. `Owin.DisconnectWatcher` gates on `IISVersion` + `UsingIntegratedPipeline` and falls back |
| `HttpRequest.Abort` | Forcibly resets the TCP connection | **OPEN** | `PlatformNotSupportedException`; Kestrel exposes an abort feature |
| `HttpRequest.InsertEntityBody` (2 overloads) | Hands the read entity back to IIS | **OPEN** (very low) | No native handler follows; the honest answer is a boundary-naming refusal or a no-op |
| `HttpRequest.HttpChannelBinding` | Extended-protection binding token | RESOLVED | Windows authentication is unsupported |
| `HttpRequest.TlsTokenBindingInfo` | Token-binding info on Win10+ | RESOLVED | The imported contract is "null when unavailable"; the port returns null |
| `HttpRequest.ContentLength` limit, `HttpBufferlessInputStream.ValidateRequestEntityLength` | Integrated answers an oversize body with a keep-alive 500 carrying `Content-Length` (IV14) | **OPEN** (low) | Measured (IV14): classic sends the same 500 body with `Connection: close` and no `Content-Length`, then closes the socket. The port takes the classic arm |
| `HttpRequest.CanValidateRequest` | Skips validation when IIS already rejected with 404/400 during Log/EndRequest | PLUMBING | No native rejection precedes managed code |
| `HttpRequest.LogonUserIdentity` | Refuses reads before `AuthenticateRequest` completes | PLUMBING | Windows auth unsupported |
| `HttpRequest.InternalRewritePath` (2 sites) | `RewriteNotifyPipeline` tells IIS the URL changed | PLUMBING | The classic `MapHandlerExecutionStep` maps from the rewritten path anyway |
| `HttpRequest.SetSkipAuthorization` | Writes the `IS_LOGIN_PAGE` server variable | PLUMBING | Pairs with `HttpContext.SetSkipAuthorizationNoDemand` above |
| `HttpContext.GetWebSocketInitStatus`, `IsWebSocketRequest`, `AcceptWebSocketRequest` | Native WebSocket module negotiation | RESOLVED | P80; the port supplies a worker-request upgrade seam and its own `IsInBeginRequestStep` fence |
| `HttpContext.Init` (`_isIntegratedPipeline`) | Latches integrated mode per context | PLUMBING | Gates the four sites below |
| `HttpContext.ApplicationInstance` setter | Refuses reassignment; installs the allocator provider natively | PLUMBING | Allocator provider is a native buffer concern |
| `HttpContext.AddError`/`ClearError` | Mirrors the error onto `NotificationContext` | PLUMBING | |
| `HttpContext.SetPrincipalNoDemand` | Pushes the principal into IIS during `AuthenticateRequest` | PLUMBING | No native consumer |
| `HttpContext.FinishPipelineRequest` cleanup | Releases `Items`/sync context earlier | PLUMBING | |
| `HttpContext.RemapHandler` | Writes the handler name/type into IIS | PLUMBING | Classic `RemapHandlerInstance` is honoured by `MapHttpHandler` |
| `HttpContext.ReportRuntimeErrorIfExists` | Lets a native handler serve `aspxerrorpath` after an init exception | PLUMBING | No native handler |
| `HttpContext.DisableNotifications` | No-op off IIS7 | PLUMBING | |
| `HttpResponse.GenerateResponseHeadersForHandler` | Generates `Cache-Control: private` and `X-AspNet-Version` into the collection | RESOLVED | `GenerateResponseHeaders` emits the equivalent classic block (P68) |
| `HttpResponse.AppendHeader` | Writes through to `Headers` instead of `_customHeaders`/`_cacheHeaders` | RESOLVED | P68 merge (`AppendManagedHeaderCollection`) |
| `HttpApplication.SendResponseExecutionStep` (raises `PreSendRequestHeaders`/`PreSendRequestContent`) | Both events run as a `SendResponse` notification step with `HttpContext.Current` set (IV12) | **OPEN** (low) | Measured (IV12): on classic both events run with `HttpContext.Current` **null**, though `((HttpApplication)sender).Context` is live and `AppendHeader` from there still reaches the wire. A module that reads `HttpContext.Current` in `PreSendRequestHeaders` gets a `NullReferenceException` on the port |
| `HttpResponse.Filter` setter, `FilterOutput`, `GetSnapshot`, `UpdateNativeResponse`, `ClearNativeResponse`, `Clear`, `EndFlush`, `Flush`, `WriteSubstBlock`, `GetHttpHeaderContentEncoding` | Native response manipulation | PLUMBING | `GetHttpHeaderContentEncoding` is already fenced for the managed collection |
| `HttpResponse.AppendToLog` | Routes to `Request.AppendToLogQueryString` | PLUMBING | No IIS log |
| `Handlers/TransferRequestHandler.ProcessRequestAsync` | Schedules a child `ExecuteUrl` for extensionless URLs | RESOLVED | The baseline row is transparent to dispatch (P85, `IisHandlerRoutes.IsTransferRequest`) |
| `Handlers/AssemblyResourceLoader.EnsureHandlerExistenceChecked` | Asks IIS to map `WebResource.axd` | RESOLVED | Fenced `#else` branch |
| `Handlers/TraceHandler.ProcessRequest` | Sets the content type explicitly | PLUMBING | Classic default is the same `text/html` |
| `httpserverutility.Execute` handler mapping | `MapIntegratedHttpHandler` | RESOLVED | The port's `MapHttpHandler` is fenced to the integrated list (P85) |
| `UI/Page.ProcessRequest` (2 sites) | `needToCallEndTrace` for IIS trace events | PLUMBING | ETW inert (P04) |
| `EtwTrace.Trace`, `RootedObjects.WorkerRequest` | Native trace/rooting | PLUMBING | P04 |

## APP-VISIBLE OPEN, ranked by likelihood a migrating application hits it

Dispositions and effort below are post-reading; the reading that settles each one is named.

1. **`Global.asax` / module subscriptions to `MapRequestHandler`, `LogRequest`,
   `PostLogRequest`** (`HttpApplication`, 12 sites plus
   `HookupEventHandlersForApplicationAndModules`). Integrated fires all three; classic has no
   such steps and the `add_` accessors throw. A module's explicit `+=` surfaces as a
   `PlatformNotSupportedException` at application init, with a message telling the operator to
   change IIS pipeline mode, which no configuration of this host can do. A `Global.asax`
   `Application_LogRequest` is worse: measured on a real classic pool the reflection hookup's
   bare `catch` drops it with no error and the application serves normally without it (IV2) —
   the exact failure mode the doctrine forbids. **Disposition: take the integrated branch**,
   and the readings make it cheap. `LogRequest`/`PostLogRequest` go **between
   `PostUpdateRequestCache` and `EndRequest`**, not after `EndRequest` as the first pass
   assumed (IV1); `_endRequestStepIndex` moves past them. `MapRequestHandler` is
   observationally identical to `PostMapRequestHandler` — IIS has already mapped the handler
   when it raises the event, and assigning `Context.Handler` from either one wins (IV4) — so
   its steps go immediately after `MapHandlerExecutionStep`, ahead of the existing
   `EventPostMapRequestHandler` steps. The response is still open and unflushed at all three
   events, so writes, `AppendHeader` and `Flush` behave exactly as in a page (IV3): no
   response-lifetime work is needed. **Reading: taken** (IV1-IV4). **Effort: lines-of-fence
   plus a scenario** — three `CreateEventExecutionSteps` calls and the `_endRequestStepIndex`
   adjustment, down from the story the first pass estimated. The hookup swallow is a separate
   one-line fail-fast that should land first.

2. **`HttpRuntime.UsingIntegratedPipeline` answers `false`** (`HttpRuntime`). Everything else
   about the port models an integrated pool — merged `<modules>` (P83), merged `<handlers>`
   (P85), request filtering (P86), retired classic sections — but the public feature-detection
   property says classic. Third-party code branches on it: three sites in the OWIN host alone
   (`ShutdownDetector`, `DisableResponseCompression`, `DisconnectWatcher`), and routing/MVC-era
   libraries use it to pick `PostResolveRequestCache` versus `PostMapRequestHandler` wireup. The
   app sees no error, just a different code path. **Disposition: decide explicitly** — this is
   the one site where the answer changes many others, so it is an ADR-sized decision, not a
   fence. Answering `true` from the public property while `UseIntegratedPipeline` stays `false`
   internally is the narrow option; it must be paired with items 1 and 3 or callers will detect
   integrated and then hit the refusals. IV16 also records a companion value a caller may pair
   with it: real IIS reports `HttpRuntime.IISVersion` as 10.0 under the integrated pool and 8.0
   under the classic pool on the same server. **Reading: taken** (IV16). **Effort: ADR +
   lines-of-fence.**

3. **`HttpContext.CurrentNotification` / `IsPostNotification`** (`HttpContext`, 4 sites).
   Modules written for integrated read these to know where they are; the port throws
   `PlatformNotSupportedException` from a property read, which is rarely guarded (IV6).
   **Disposition: shim**, and the map is now measured rather than inferred — IV6 gives the
   value every classic managed event reports, including the two the classic pipeline has no
   step for. `ApplicationStepManager` already tracks step ranges (`_beginRequestStepEndIndex`
   for the WebSocket fence), so the same mechanism carries the notification. **Reading: taken**
   (IV6). **Effort: new seam (small).**

4. **Writable `Request.Headers`** (`HttpRequest.EnsureHeaders`). Integrated let an application
   or module mutate request headers and have downstream code see the change; the port calls
   `MakeReadOnly()`, so `Set`/`Add`/`Remove` throw (IV9). Common in SSO, header-based
   impersonation and reverse-proxy shims. **Disposition: shim**, the mirror of P68 — without a
   native block the managed collection is simply the store. The contract to reproduce is
   measured: `Set` replaces, `Add` appends comma-joined, `Remove` deletes, `Clear` throws
   `NotSupportedException` (IV7); `ServerVariables["HTTP_*"]` follows a mutation **only if the
   server-variable collection already exists** when the mutation happens, and `Request.Url`
   follows a `Host` rewrite **only if `Url` has not yet been read** (IV8). Those two caveats are
   Framework's own laziness, not IIS behavior, so the port reproduces them for free by keeping
   `SynchronizeHeader`'s existing shape. Typed known-header accessors do not follow the
   collection (IV8). **Reading: taken** (IV7-IV9). **Effort: lines-of-fence plus a scenario.**

5. **`HttpApplication.OnExecuteRequestStep`** (`HttpApplication`). Step-wrapping is how
   request-scoped instrumentation (APM agents, diagnostic middleware) attaches. The port
   throws, yet `ExecuteStepImpl` already invokes `_stepInvoker` in both step managers — only the
   gate blocks it. **Disposition: take the integrated branch.** **Reading: not needed.**
   **Effort: lines-of-fence.**

Remaining open items, in descending likelihood:

- **`Response.AddOnSendingHeaders`** — Katana probes it reflectively and silently degrades.
  Measured firing point is the `SendResponse` notification, before the head is emitted (IV11),
  which is the port's own flush commit point. **Shim**, lines-of-fence.
- **`Response.ClientDisconnectedToken`, `Request.Abort`** — long-poll and streaming apps;
  Kestrel exposes both capabilities. **Shim.**
- **`HideRequestResponse` during `Application_Start`/`Init`** — the port is more permissive
  than what the audience observed. **Take the integrated branch** for least astonishment.
- **`PreSendRequestHeaders`/`PreSendRequestContent` context** — new, found by measurement:
  `HttpContext.Current` is null in both events on classic while integrated supplies it (IV12).
  `((HttpApplication)sender).Context` is live in both, so the fix is to restore
  `HttpContext.Current` around the raise. **Shim**, lines-of-fence.
- **`DefaultAuthenticationModule` hooking `DefaultAuthentication`** — measured: on integrated
  `Context.User` is null through `AuthenticateRequest` and established by
  `PostAuthenticateRequest`; on classic it is already set when `AuthenticateRequest` runs
  (IV15). A module reading `Context.User` at `AuthenticateRequest` therefore sees null on the
  audience's server and a principal here. **Take the integrated branch** (hook
  `PostAuthenticateRequest`); needs care against `RoleManagerModule`'s position in the module
  list.
- **`ThrowIfEventBindingDisallowed`** — late binding is accepted and silently inert on classic,
  and throws `InvalidOperationException` naming module initialization on integrated (IV10).
  **Fail-fast**, reusing Framework's own message.
- **Oversize request body** — integrated answers keep-alive with `Content-Length`; classic
  closes the connection and omits it (IV14). **Take the integrated branch** (drop the
  `CloseConnectionAfterError` call off IIS7), lines-of-fence.
- `Response.SubStatusCode`; `CallHandlerExecutionStep`'s `IsHandlerExecutionDenied` 403;
  `HostingEnvironment.MaxConcurrent*PerCPU` (4 sites, one behavior — keep the refusal, reword
  it); `Request.InsertEntityBody`; `Response.PushPromise` (silent no-op, needs only a
  compatibility row); `DefaultAuthentication.Authenticate` and `DefaultHttpHandler` (inverse
  gates the audience never exercised).

## Framework readings

Captured 2026-08-30 on `winbox`, IIS 10.0 / Windows 11, .NET Framework 4.8.9344 (4.8.1). One
Web Forms application (`Global.asax` subscribing every `Application_*` pipeline event into a
static trace, plus `App_Code` probes) served from one physical directory by two sites: port
8123 on an **Integrated** v4.0 pool and port 8124 on a **Classic** v4.0 pool, so the only
variable is pipeline mode. Requests were raw sockets on the box; the classic leg is the
contrast the audit needs, because it is what the port reproduces today. Anonymous
authentication, `customErrors mode="Off"`, `debug="false"`.

### Pipeline shape

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| IV1 | `GET /probe.aspx`, integrated, trace every managed event | `BeginRequest`, `AuthenticateRequest`, `PostAuthenticateRequest`, `AuthorizeRequest`, `PostAuthorizeRequest`, `ResolveRequestCache`, `PostResolveRequestCache`, `MapRequestHandler`, `PostMapRequestHandler`, `AcquireRequestState`, `PostAcquireRequestState`, `PreRequestHandlerExecute`, page, `PostRequestHandlerExecute`, `ReleaseRequestState`, `PostReleaseRequestState`, `UpdateRequestCache`, `PostUpdateRequestCache`, **`LogRequest`, `PostLogRequest`, `EndRequest`**, `PreSendRequestHeaders`, `PreSendRequestContent`, `RequestCompleted` | `LogRequest`/`PostLogRequest` precede `EndRequest`; the pre-send events follow it. The first pass had them after `EndRequest`. |
| IV2 | Same `Global.asax` on the classic pool | `Application_MapRequestHandler`, `Application_LogRequest`, `Application_PostLogRequest` never appear in the trace. No exception page, no 500, no entry anywhere; every other event fires and the request returns 200 | Classic drops integrated-only `Application_*` handlers silently. This is what the port does today. |
| IV3 | `?w=1`: write and `AppendHeader` at `LogRequest`, `PostLogRequest` and `EndRequest`, plus an explicit `Flush` at `LogRequest`, integrated | All three writes reach the client, in event order, appended after the page body. `AppendHeader` at `LogRequest` succeeds and `X-At-LogRequest: 1` is on the wire. The `Flush` commits the head and switches the response to chunked; the later `AppendHeader` at `EndRequest` then throws `HttpException: Server cannot append header after HTTP headers have been sent` | Without an explicit flush the response is still open and buffered at all three events; nothing about response lifetime distinguishes them from earlier stages. |
| IV12 | Trace `HttpContext.Current` and `((HttpApplication)sender).Context` in `PreSendRequestHeaders`/`PreSendRequestContent`, both pools | Integrated: `Current` present, `sender.Context` present. Classic: `Current` **null**, `sender.Context` present. `AppendHeader` through `sender.Context` reaches the wire in both | The pre-send events lose `HttpContext.Current` on classic only. |
| IV15 | Trace `Context.User` at each event, both pools, anonymous auth | Integrated: null at `BeginRequest` and `AuthenticateRequest`; `WindowsPrincipal`/`WindowsIdentity`, empty name, `IsAuthenticated=false` from `PostAuthenticateRequest` onward. Classic: already that principal at `AuthenticateRequest` | The default principal is established one stage later on integrated. |
| IV16 | `HttpRuntime.UsingIntegratedPipeline` and `HttpRuntime.IISVersion` at `Application_Start`, both pools | Integrated: `True`, `10.0`. Classic: `False`, `8.0` | Same IIS 10 server; the classic pool reports a downlevel IIS version. |

### Handler mapping

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| IV4 | `?map=set`: assign `Context.Handler = new MarkerHandler()` inside `Application_MapRequestHandler`, integrated | `Context.Handler` is **already** `probe_aspx` when `MapRequestHandler` is raised. The assignment sticks: `PostMapRequestHandler` and `PreRequestHandlerExecute` see `MarkerHandler`, and the response body is `MARKER-HANDLER-RAN` | IIS maps the handler before raising the managed event; the event is a substitution point, not the mapping itself. |
| IV4b | `?map=post-set`: same assignment at `PostMapRequestHandler` | Identical outcome — `PreRequestHandlerExecute` sees `MarkerHandler`, body is `MARKER-HANDLER-RAN` | `MapRequestHandler` and `PostMapRequestHandler` are observationally interchangeable for handler substitution. |
| IV5 | `?map=remap`: `Context.RemapHandler(...)` inside `Application_MapRequestHandler` | `InvalidOperationException: 'HttpContext.RemapHandler' can only be invoked before 'HttpApplication.MapRequestHandler' event is raised.` The page handler runs unchanged | `RemapHandler`'s window closes when `MapRequestHandler` is raised. |
| IV5b | `?map=remap-early`: `Context.RemapHandler(...)` at `PostResolveRequestCache` | Succeeds; `MapRequestHandler` already reports `MarkerHandler` as the handler | The remap pre-empts IIS mapping rather than overriding it afterwards. |

### Request headers

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| IV7 | `?hdr=1`, integrated: at `BeginRequest` `Set("X-Probe")`, `Add("X-Multi","a")`, `Add("X-Multi","b")`, `Remove("User-Agent")`, `Clear()`, `Set("Host")` | All succeed except `Clear()`, which throws `NotSupportedException: Specified method is not supported.` `X-Multi` reads back as `a,b` | The collection is fully writable except `Clear`; `Add` appends comma-joined. |
| IV8 | Read the mirrors after IV7 (server variables already materialized) and after `?hdr=2` (mutation before any read) | With the server-variable collection already created: `HTTP_X_PROBE` shows the set value and `HTTP_USER_AGENT` becomes null after the remove. With no prior read: `HTTP_USER_AGENT` and `Request.UserAgent` still show the original value, while `Request.Url` **does** pick up the rewritten `Host` (`http://rewritten.example:8123/probe.aspx?hdr=2`). `Request.ContentType` does not follow a `Content-Type` set on a GET | Mirrors are updated in place, not recomputed: propagation depends on whether the mirror collection existed at mutation time and whether the derived value was already cached. Typed known-header accessors do not follow the collection. |
| IV9 | Read `NameValueCollection.IsReadOnly` on `Request.Headers`, both pools | Integrated `False`, classic `True`; both are `HttpHeaderCollection` | The classic arm is the `MakeReadOnly()` call in `EnsureHeaders`, which is what the port takes. |

### Refused APIs and response shape

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| IV6 | Read `Context.CurrentNotification`/`IsPostNotification` at every managed event, both pools | Classic: `PlatformNotSupportedException` everywhere. Integrated map — `BeginRequest`→`BeginRequest`; `AuthenticateRequest`/`PostAuthenticateRequest`→`AuthenticateRequest` (post=false/true); `AuthorizeRequest`/`Post…`→`AuthorizeRequest`; `ResolveRequestCache`/`Post…`→`ResolveRequestCache`; `MapRequestHandler`/`Post…`→`MapRequestHandler`; `AcquireRequestState`/`Post…`→`AcquireRequestState`; `PreRequestHandlerExecute`→`PreExecuteRequestHandler` (post=false); page body and `PostRequestHandlerExecute`→`ExecuteRequestHandler` (post=false/true); `ReleaseRequestState`/`Post…`→`ReleaseRequestState`; `UpdateRequestCache`/`Post…`→`UpdateRequestCache`; `LogRequest`/`PostLogRequest`→`LogRequest`; `EndRequest`→`EndRequest`; pre-send events→`SendResponse` | Every classic managed event has an unambiguous notification value; `Post*` events are the post phase of their base notification. |
| IV10 | `?bind=1`: `app.LogRequest += …` and `app.EndRequest += …` from `Application_BeginRequest`, both pools | Integrated: both throw `InvalidOperationException: Event handlers can only be bound to HttpApplication events during IHttpModule initialization.` Classic: the `LogRequest` bind throws `PlatformNotSupportedException: This operation requires IIS integrated pipeline mode.`, the `EndRequest` bind **succeeds** and the handler never runs | Classic accepts a late binding and silently ignores it; integrated refuses it. |
| IV11 | `?sh=1`: `Response.AddOnSendingHeaders(cb)` at `BeginRequest`, both pools | Integrated: registration succeeds and the callback runs once, inside notification `SendResponse`, with status still 200. Classic: `PlatformNotSupportedException: This operation requires IIS integrated pipeline mode.` | The callback point is the head-commit boundary. |
| IV13 | `Response.Redirect("/target.aspx", false)` from a page, both pools, raw wire | Byte-identical 302 heads and bodies, both carrying `Content-Type: text/html; charset=utf-8`, `Location: /target.aspx`, `Content-Length: 129` and the Object-moved body | The integrated-only `ContentType = "text/html"` line in `Redirect` is unobservable: the classic default already produces it. |
| IV14 | `POST` 8 KB to a folder with `maxRequestLength="4"` (KB), no `Connection: close`, both pools | Both return 500 with the same ASP.NET error body. Integrated: `Content-Length: 4802`, no `Connection` header, **socket stays open** (client read timed out waiting). Classic: `Connection: close`, **no `Content-Length`**, server closes the socket immediately. 1 KB control returns 200 keep-alive on both | The classic `CloseConnectionAfterError()` arm is observable on the wire as a connection reset and a missing `Content-Length`. |

The rig (two sites, one shared application directory, raw-socket reader) was removed after the
readings; nothing durable was left on the host.
