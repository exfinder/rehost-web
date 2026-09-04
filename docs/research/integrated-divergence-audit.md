# Managed integrated-vs-classic divergence audit

The port runs the classic managed pipeline ([ADR 0001](../adr/0001-runtime-compatibility-model.md)),
but the migrating audience ran IIS integrated mode: every integrated-mode branch in the
imported source is behavior they observed and this runtime never executes. This audit
enumerates those branches so each one ends as take-integrated-branch, shim, or a fail-fast
diagnostic naming the boundary — never a silent classic-mode difference
([backlog](../backlog.md)).

Research only. Current support lives in the [compatibility map](../compatibility.md);
priority lives in the backlog. Readings IV1-IV30 below were taken after the first pass and
have settled every entry they touch; rows carry the reading id that decides them.

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
rather than inferred: [readings IV1-IV30](#framework-readings) run one application under an
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
| PLUMBING | 32 | 16 |
| RESOLVED | 25 | 11 |
| APP-VISIBLE OPEN | 18 | 8 |
| Total | 75 | 35 |

The 18 open `UseIntegratedPipeline` sites collapse to 12 distinct behaviors: the
`HideRequestResponse` pairs account for 4 and the `HostingEnvironment` throttle accessors for
another 4. The `MapRequestHandler`/`LogRequest`/`PostLogRequest` event family (12 sites) and
`HookupEventHandlersForApplicationAndModules` closed with job 3 (ledger P90). Job 4
closed six more `UseIntegratedPipeline` sites — `CurrentNotification`/`IsPostNotification`,
`OnExecuteRequestStep` and `ThrowIfEventBindingDisallowed` — and four members from the
other-gates table: `EnsureHeaders`, `AddOnSendingHeaders`, the oversize-body pair and the
pre-send raise (ledger P91). Job 5 closed the build half of the remainder: six more
`UseIntegratedPipeline` sites — the two `HideRequestResponse` pairs, `CallHandlerExecutionStep`
and `DefaultAuthenticationModule.Init` — and four members from the other-gates table
(`SubStatusCode`, `ClientDisconnectedToken`, `Request.Abort`, `InsertEntityBody`), plus the
error-page timing P90 left open (ledger P92). Two of those closed as non-divergences rather
than as code: IV27 and IV29. One first-pass open item — the `Response.Redirect` content type —
closed as PLUMBING once measured (IV13); one new open member was found by measurement (IV12).
What remains open is job 6's fail-fast half: `MaxConcurrent*PerCPU`,
`DefaultAuthentication.Authenticate` and `DefaultHttpHandler`.

## `HttpApplication.cs` — 34 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `IsContainerInitalizationAllowed` | True while re-running `InitInternal` after IIS event registration | PLUMBING | Module step containers exist only for native notification dispatch |
| `ThrowIfEventBindingDisallowed` | `InvalidOperationException` "Event handlers can only be bound to HttpApplication events during IHttpModule initialization" (IV10) | RESOLVED | The port raises the same refusal once `InitInternal` has completed, since `_initSpecialCompleted` is never set here (ledger P91) |
| `FindISessionStateModule` | Returns the session module so `EnsureReleaseState` can release before a child request | PLUMBING | Child `Server.Execute` shares the parent's session; the release exists for `TransferRequest`, which is unsupported (P62) |
| `AcquireNotifcationContextLock` / `ReleaseNotifcationContextLock` (2 sites) | `Debug.Assert` | PLUMBING | Debug-only; callers are integrated-only |
| `AsyncResult` get/set (2 sites) | Stores the pending result on `NotificationContext` instead of `_ar` | PLUMBING | Per-notification storage; classic uses the field |
| `MapRequestHandler` event add/remove (2 sites) | Fires after IIS has already mapped the handler, immediately before `PostMapRequestHandler`, with identical observable state (IV1, IV4) | RESOLVED | Gate fenced off `NETFRAMEWORK` and the steps added after `MapHandlerExecutionStep` (ledger P90) |
| `LogRequest` event add/remove (2 sites) | Fires after `PostUpdateRequestCache` and **before** `EndRequest` (IV1) | RESOLVED | Steps added inside the early-end jump target, which stays ahead of them (IV17, ledger P90) |
| `PostLogRequest` event add/remove (2 sites) | Fires between `LogRequest` and `EndRequest` (IV1) | RESOLVED | Same |
| `AddOnMapRequestHandlerAsync` (2 overloads) | Async form of the above | RESOLVED | Same; `CreateEventExecutionSteps` already emits the async steps (ledger P90) |
| `AddOnLogRequestAsync` (2 overloads) | Async form | RESOLVED | Same |
| `AddOnPostLogRequestAsync` (2 overloads) | Async form | RESOLVED | Same |
| `ProcessSpecialRequest` enter/exit (2 sites) | Sets `HttpContext.HideRequestResponse`, so `Context.Request`/`Response` throw inside `Application_Start` (IV22) | RESOLVED | Both gates fenced off `NETFRAMEWORK`; `Application_End` and `Session_End` pass no context and stay unaffected (ledger P92) |
| `InitInternal` module build | `InitIntegratedModules` from the merged `system.webServer/modules` list | RESOLVED | Port's `InitModules` is fenced to build the same integrated collection (P83) |
| `InitInternal` `HideRequestResponse` around `Init()` (2 sites) | Request/response hidden during `Init()` and module init (IV22) | RESOLVED | Both gates fenced off, and the classic arm's `InitModules()` call gained the same hiding, which is where the port initialises modules (ledger P92) |
| `InitInternal` step manager | `PipelineStepManager` | RESOLVED | Classic `ApplicationStepManager` is the decision ([ADR 0001](../adr/0001-runtime-compatibility-model.md)) |
| `DisposeInternal` module key | Tracks `_currentModuleCollectionKey` so a module can unregister during `Dispose` | PLUMBING | Event maps exist only in integrated |
| `HookupEventHandlersForApplicationAndModules` | A failing `add_XXX` rethrows; classic swallows it | RESOLVED | Measured on a real classic pool (IV2): the handlers were dropped with no error, no 500 and no log entry. The port rethrows (ledger P90); the failure surfaces as a 500 on every request, since the refusal is raised while the application instance is built |
| `OnExecuteRequestStep` | Registers a wrapper around every pipeline step | RESOLVED | The gate is fenced off and `ExecuteStepImpl`'s `_stepInvoker` carries the wrapper through the classic list (ledger P91) |
| `AssignContext` | `Debug.Assert` | PLUMBING | |
| `AsyncAppEventHandlersTable.AddHandler` | Also adds an `AsyncEventExecutionStep` to the module's event map | PLUMBING | Event maps are the native dispatch table |
| `AsyncEventExecutionStep` ctor chaining | Passes the mode to an overload that ignores the parameter | PLUMBING | Parameter unused in both arms |
| `CallHandlerExecutionStep.Execute` | `IIS7WorkerRequest.IsHandlerExecutionDenied()` → 403 with `PageForbiddenErrorFormatter` | RESOLVED | Not a pipeline-mode divergence: IV29 measured both native denial paths refusing ahead of managed code and identically on the two pools — `security/authorization` `Deny` gives 401.2 from `UrlAuthorizationModule`, `handlers accessPolicy="Read"` gives 403.1 from IIS Web Core. The port reads neither section, which is the Later entry under MH36 |
| `CallFilterExecutionStep.Execute` | Disables the `LogRequest` notification after `UpdateRequestCache` | PLUMBING | Notification suppression |
| `StepManager.CompleteRequest` | Marks `NotificationContext.RequestCompleted` | PLUMBING | |

## `HttpContext.cs` — 5 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `SetSkipAuthorizationNoDemand` | Persists into the `IS_LOGIN_PAGE` server variable so IIS skips its own authorization | RESOLVED | Native `system.webServer` authorization is unimplemented and the section ignored; managed `SkipAuthorization` is unaffected (compatibility: URL/file authorization) |
| `CurrentNotification` get/set (2 sites) | Returns the executing `RequestNotification`; the full managed-event-to-notification map is measured in IV6 | RESOLVED | Answered from a notification table built beside the classic step list; `RequestCompleted` reads `EndRequest` and module `Init` on a request instance reads `BeginRequest` (IV18); where Framework throws `NullReferenceException` (`Application_Start`, the special instance) the port raises `InvalidOperationException` naming the boundary (ledger P91) |
| `IsPostNotification` get/set (2 sites) | Post-phase flag; every `Post*` managed event reports `true` on its base notification (IV6) | RESOLVED | Same table (ledger P91) |

## `HttpRuntime.cs` — 4 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `UsingIntegratedPipeline` (public) | `true` under an integrated pool; `false` under a classic pool on the same IIS 10, which also reports `HttpRuntime.IISVersion` as 8.0 rather than 10.0 (IV16) | RESOLVED | The public pair now answers `true`/`10.0` as the integrated pool the port already models for modules (P83), handlers (P85) and request filtering (P86); the internal `UseIntegratedPipeline` stays `false` ([ADR 0013](../adr/0013-integrated-pipeline-identity.md)) |
| `UseIntegratedPipeline` (internal getter) | Definition site | PLUMBING | |
| `Dispose` drain | `PipelineRuntime.WaitForRequestsToDrain()` vs active-count spin | PLUMBING | Process replacement owns shutdown ([ADR 0012](../adr/0012-runtime-initiated-restart.md)) |
| `ProcessRequest(HttpWorkerRequest)` | Refuses the classic entry point | RESOLVED | Inverse gate; this entry point is the port's engine ([ADR 0001](../adr/0001-runtime-compatibility-model.md)) |

## `HttpResponse.cs` — 2 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `Redirect` (VSO 360276) | Sets `ContentType = "text/html"` before writing the Object-moved body | PLUMBING | Measured byte-identical 302 responses from integrated and classic pools on the same IIS 10 (IV13): the classic default already emits `Content-Type: text/html; charset=utf-8` |
| `PushPromise` | Calls `IIS7WorkerRequest.PushPromise` | RESOLVED | The imported `catch (PlatformNotSupportedException)` swallows the refusal, so the call is a silent no-op, consistent with Framework's own fire-and-forget contract; compatibility row (`Response.PushPromise`); one-off run over Kestrel on 2026-09-03: a `Trace="true"` page calling `PushPromise("~/styles.css")` returned 200 with the trace carrying "Push promise is not supported" and the `Requires_Iis_Integrated_Mode` text, and a non-virtual path raised `Invalid_path_for_push_promise` before the integrated gate |

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
| `FileAuthorizationModule.CheckFileAccessForUser` | `s_Enabled = true` unconditionally | RESOLVED | Module is inert (P84). Note the inconsistency: unlike `UrlAuthorizationModule` this classic arm still reads the retired `<httpModules>` section, so the public API silently reports access granted; compatibility row (URL/file authorization) |
| `WindowsAuthenticationModule.OnEnter` | Takes the principal IIS already set; classic reads `LOGON_USER`/`AUTH_TYPE` | RESOLVED | Registered but inert; `mode="Windows"` fails activation (P84) |
| `RoleManagerModule.OnEnter` (2 sites) | `DisableNotifications(EndRequest)` when roles are off or uncached | PLUMBING | Perf only |
| `DefaultAuthenticationModule.Authenticate` event add | Refuses the subscription | **OPEN** (low) | Inverse gate: the port accepts an event the audience could not use |
| `DefaultAuthenticationModule.Init` | Hooks `PostAuthenticateRequest`; classic hooks `DefaultAuthentication` | RESOLVED | Not observable here: IV27 re-measured with `authentication mode` `None` and `Forms` and the two pools are identical — `Context.User` null at `AuthenticateRequest`, a `GenericIdentity` at `PostAuthenticateRequest` — because the classic `DefaultAuthentication` step sits between the two events. Only `mode="Windows"` diverges (IV15), and that mode fails activation (P84). `HttpApplication.DefaultAuthentication` is `internal`, so no application can subscribe |

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
| `HttpRequest.EnsureHeaders` | Collection is writable: `Set`/`Add`/`Remove` succeed, `Add` appends comma-joined, `Clear` throws `NotSupportedException` (IV7); mutations feed `ServerVariables` and `Request.Url` with the caveats in IV8 | RESOLVED | `MakeReadOnly()` is fenced off and the managed collection is the store, with both IV8 laziness caveats reproduced and the IV19 one measured after the fact; the `GetSimpleServerVar` shortcuts still answer from the worker request (ledger P91) |
| `HttpRequest.GetServerVars` | Collection is writable | RESOLVED | `ServerVariables.Set` unavailable, stated in P76 and the compatibility row |
| `HttpResponse.Headers` | Native header block backs the collection | RESOLVED | P68, readings H1–H16; fenced so the managed collection is the store |
| `HttpResponse.SubStatusCode` get/set | Stored value, `0` by default, with no effect on the response bytes (IV26) | RESOLVED | Both `IIS7WorkerRequest` type tests fenced off, so get and set work against the existing field and the `_headersWritten` refusal stays. The port has no IIS log, so nothing consumes the value (ledger P92) |
| `HttpResponse.AddOnSendingHeaders` | Per-request pre-send callback; measured firing inside the `SendResponse` notification with the response still 200 and headers not yet emitted (IV11) | RESOLVED | The queue fires in `WriteHeaders` after the pre-send headers event and before header generation, so `OwinCallContext.RegisterForOnSendingHeaders`'s reflective probe no longer fails (ledger P91) |
| `HttpResponse.ClientDisconnectedToken` | Cancelable for the whole request and past its end, but signalled only when the server next touches the connection (IV24) | RESOLVED | A worker-request seam answers Kestrel's `RequestAborted`. Boundary: the port is eager where IIS never signalled an idle request (ledger P92). `Owin.DisconnectWatcher` is back to its upstream shape |
| `HttpRequest.Abort` | Resets the connection without ending the request; managed code runs on and the next flush raises (IV25) | RESOLVED | The same seam calls `HttpContext.Abort()` and marks the client gone. Boundary: the flush after the abort does not raise here (ledger P92) |
| `HttpRequest.InsertEntityBody` (2 overloads) | Both overloads return with no effect; the response is unchanged (IV28) | RESOLVED | A no-op behind the fence, after Framework's own argument checks in Framework's own order (ledger P92) |
| `HttpRequest.HttpChannelBinding` | Extended-protection binding token | RESOLVED | Windows authentication is unsupported |
| `HttpRequest.TlsTokenBindingInfo` | Token-binding info on Win10+ | RESOLVED | The imported contract is "null when unavailable"; the port returns null |
| `HttpRequest.ContentLength` limit, `HttpBufferlessInputStream.ValidateRequestEntityLength` | Integrated answers an oversize body with a keep-alive 500 carrying `Content-Length` (IV14) | RESOLVED | Both `CloseConnectionAfterError()` calls are fenced off, so the refusal carries `Content-Length` and the connection serves the next request (ledger P91) |
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
| `HttpContext.RemapHandler` | Writes the handler name/type into IIS, and refuses a call at or after the `MapRequestHandler` notification (IV5) | RESOLVED | Classic `RemapHandlerInstance` is honoured by `MapHttpHandler`; the port now raises the same `InvalidOperationException` at or after the map step rather than storing a value nobody reads (ledger P90) |
| `HttpContext.ReportRuntimeErrorIfExists` | Lets a native handler serve `aspxerrorpath` after an init exception | PLUMBING | No native handler |
| `HttpContext.DisableNotifications` | No-op off IIS7 | PLUMBING | |
| `HttpResponse.GenerateResponseHeadersForHandler` | Generates `Cache-Control: private` and `X-AspNet-Version` into the collection | RESOLVED | `GenerateResponseHeaders` emits the equivalent classic block (P68) |
| `HttpResponse.AppendHeader` | Writes through to `Headers` instead of `_customHeaders`/`_cacheHeaders` | RESOLVED | P68 merge (`AppendManagedHeaderCollection`) |
| `HttpApplication.SendResponseExecutionStep` (raises `PreSendRequestHeaders`/`PreSendRequestContent`) | Both events run as a `SendResponse` notification step with `HttpContext.Current` set (IV12) | RESOLVED | The port measured `null` in both events before the fix; the raise now restores `HttpContext.Current` and reports notification `SendResponse` (ledger P91) |
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
   assumed (IV1); `_endRequestStepIndex` stays ahead of them, because integrated raises both
   Log events after every early end too (IV17). `MapRequestHandler` is
   observationally identical to `PostMapRequestHandler` — IIS has already mapped the handler
   when it raises the event, and assigning `Context.Handler` from either one wins (IV4) — so
   its steps go immediately after `MapHandlerExecutionStep`, ahead of the existing
   `EventPostMapRequestHandler` steps. The response is still open and unflushed at all three
   events, so writes, `AppendHeader` and `Flush` behave exactly as in a page (IV3): no
   response-lifetime work is needed. **Reading: taken** (IV1-IV4). **Effort: lines-of-fence
   plus a scenario** — three `CreateEventExecutionSteps` calls, down from the story the first
   pass estimated. The hookup swallow is a separate one-line fail-fast that should land first.
   **Landed** (job 3, ledger P90), together with the `RemapHandler` window (IV5).

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
   lines-of-fence.** **Landed as [ADR 0013](../adr/0013-integrated-pipeline-identity.md)**:
   the public pair answers `true`/`10.0`, the internal flag stays `false`.

3. **`HttpContext.CurrentNotification` / `IsPostNotification`** (`HttpContext`, 4 sites).
   Modules written for integrated read these to know where they are; the port throws
   `PlatformNotSupportedException` from a property read, which is rarely guarded (IV6).
   **Disposition: shim**, and the map is now measured rather than inferred — IV6 gives the
   value every classic managed event reports, including the two the classic pipeline has no
   step for. `ApplicationStepManager` already tracks step ranges (`_beginRequestStepEndIndex`
   for the WebSocket fence), so the same mechanism carries the notification. **Reading: taken**
   (IV6). **Effort: new seam (small).** **Landed** (job 4, ledger P91).

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
   **Landed** (job 4, ledger P91).

5. **`HttpApplication.OnExecuteRequestStep`** (`HttpApplication`). Step-wrapping is how
   request-scoped instrumentation (APM agents, diagnostic middleware) attaches. The port
   throws, yet `ExecuteStepImpl` already invokes `_stepInvoker` in both step managers — only the
   gate blocks it. **Disposition: take the integrated branch.** **Reading: not needed.**
   **Effort: lines-of-fence.** **Landed** (job 4, ledger P91).

Remaining open items, in descending likelihood:

- **`Response.AddOnSendingHeaders`** — Katana probes it reflectively and silently degrades.
  Measured firing point is the `SendResponse` notification, before the head is emitted (IV11),
  which is the port's own flush commit point. **Shim**, lines-of-fence. **Landed** (job 4, ledger P91).
- **`Response.ClientDisconnectedToken`, `Request.Abort`** — long-poll and streaming apps;
  Kestrel exposes both capabilities. **Shim.** **Landed** (job 5, ledger P92), with two
  boundaries: the token is eager where IIS waited for the server's next write (IV24), and the
  flush after an abort does not raise (IV25).
- **`HideRequestResponse` during `Application_Start`/`Init`** — the port is more permissive
  than what the audience observed. **Take the integrated branch** for least astonishment.
  **Landed** (job 5, ledger P92); IV22's first-instance case has no counterpart here, since
  `InitSpecial` runs with `appContext == IntPtr.Zero` and initialises no modules.
- **`PreSendRequestHeaders`/`PreSendRequestContent` context** — new, found by measurement:
  `HttpContext.Current` is null in both events on classic while integrated supplies it (IV12).
  `((HttpApplication)sender).Context` is live in both, so the fix is to restore
  `HttpContext.Current` around the raise. **Shim**, lines-of-fence. **Landed**
  (job 4, ledger P91).
- **`DefaultAuthenticationModule` hooking `DefaultAuthentication`** — **closed as a
  non-divergence** (job 5). IV15's contrast was `mode="Windows"`, which fails activation here
  (P84). IV27 re-measured under `None` and `Forms`: both pools report `null` at
  `AuthenticateRequest` and a `GenericIdentity` at `PostAuthenticateRequest`, because the
  classic `DefaultAuthentication` step falls between the two events. The event itself is
  `internal`, so an `Application_DefaultAuthentication` method never fires on either pool. No
  code change; compatibility row only.
- **`ThrowIfEventBindingDisallowed`** — late binding is accepted and silently inert on classic,
  and throws `InvalidOperationException` naming module initialization on integrated (IV10).
  **Fail-fast**, reusing Framework's own message. **Landed** (job 4, ledger P91).
- **Oversize request body** — integrated answers keep-alive with `Content-Length`; classic
  closes the connection and omits it (IV14). **Take the integrated branch** (drop the
  `CloseConnectionAfterError` call off IIS7), lines-of-fence. **Landed** (job 4,
  ledger P91).
- `Response.SubStatusCode` and `Request.InsertEntityBody` — **landed** (job 5, ledger P92):
  the substatus round-trips with no wire effect (IV26), and both `InsertEntityBody` overloads
  return after their argument checks (IV28).
- `CallHandlerExecutionStep`'s `IsHandlerExecutionDenied` 403 — **closed as a non-divergence**
  (job 5). IV29: `security/authorization` `Deny` answers 401.2 from `UrlAuthorizationModule`
  and `handlers accessPolicy="Read"` answers 403.1 from IIS Web Core, both ahead of managed
  code and both identical across the pools. The port's gap is that it reads neither section,
  which stays the Later entry under MH36.
- `HostingEnvironment.MaxConcurrent*PerCPU` (4 sites, one behavior — keep the refusal, reword
  it); `DefaultAuthentication.Authenticate` and `DefaultHttpHandler` (inverse gates the
  audience never exercised) — job 6.

## Job map

The [backlog](../backlog.md) closure plan's six jobs cover the open items as
follows; this map is the authoritative item-to-job assignment.

- **Job 1 (doc rows) — landed:** `Response.PushPromise` silent no-op;
  `FileAuthorizationModule` dead-path note (other-gates table). Both are
  compatibility rows; the no-op was verified by a one-off run (rung 0, no
  standing test).
- **Job 2 (ADR 0013) — landed:** ranked item 2, `UsingIntegratedPipeline`
  identity ([ADR 0013](../adr/0013-integrated-pipeline-identity.md)).
- **Job 3 (event family) — landed:** ranked item 1 — the three events, their
  sync and async accessors, and the hookup-swallow fail-fast that landed
  first; plus the `HttpContext.RemapHandler` window (IV5), added to the job
  because the map step is what closes it (ledger P90).
- **Job 4 (shim batch) — landed:** ranked items 3 (`CurrentNotification`/
  `IsPostNotification`), 4 (writable `Request.Headers`), 5
  (`OnExecuteRequestStep`); `AddOnSendingHeaders`; `PreSendRequestHeaders`/
  `Content` context restore; oversize-body fence;
  `ThrowIfEventBindingDisallowed` fail-fast (ledger P91). Two boundaries the
  port keeps: a notification read outside a step raises
  `InvalidOperationException` naming it, and the six server variables
  `GetSimpleServerVar` answers without populating the collection —
  `HTTP_USER_AGENT` among them — do not follow a header mutation, where IV8
  measured integrated's `HTTP_USER_AGENT` doing so.
- **Job 5 (remainder, build half) — landed:** readings IV21-IV30 first, then
  the error page and status at the early-end jump (IV21, closing P90's
  boundary), `HideRequestResponse` (IV22), the `WEBSOCKET_VERSION` server
  variable (IV23), `ClientDisconnectedToken` and `Request.Abort` (IV24, IV25),
  `Response.SubStatusCode` (IV26), `Request.InsertEntityBody` (IV28) and
  `HostingEnvironment.StopListening` from the host's stopping notification
  (IV30); Katana's `DisconnectWatcher` went back to upstream (ledger P92). Two
  items closed as non-divergences with a row and no code: the
  `DefaultAuthenticationModule` hook position (IV27) and
  `CallHandlerExecutionStep`'s 403 (IV29). Boundaries the port keeps: the
  disconnect token is eager, the flush after `Request.Abort` does not raise,
  and the substatus has no consumer.
- **Job 6 (remainder, fail-fast half):** `MaxConcurrent*PerCPU` refusal
  reword; the inverse gates (`DefaultAuthentication.Authenticate`,
  `DefaultHttpHandler`) — decide keep-or-document, then row each.

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
| IV17 | Nine early-end stimuli on the integrated pool, tracing every managed event per request (`probe.aspx` `Page_Load` for the page stimuli, `Application_BeginRequest` for the `b*` ones); taken 2026-09-03 | plain → 200, full IV1 order. `Response.End()` in page → 200, `PreRequestHandlerExecute`, `LogRequest`, `PostLogRequest`, `EndRequest`, pre-send pair (`PostRequestHandlerExecute` through `PostUpdateRequestCache` skipped). `CompleteRequest()` in page → same. `throw` in page → 500, `Error:HttpUnhandledException`, `LogRequest` with `Response.StatusCode` already 500, `PostLogRequest`, `EndRequest`, pre-send pair. `Response.Redirect(url)` → 302, End's shape, status 302 at `LogRequest`. `Response.Redirect(url, false)` → 302, full order. `Response.End()` at `BeginRequest` → 200, `BeginRequest`, `LogRequest`, `PostLogRequest`, `EndRequest`, pre-send pair (`MapRequestHandler` skipped). `CompleteRequest()` at `BeginRequest` → same. `throw` at `BeginRequest` → 500, `Error:InvalidOperationException`, `LogRequest` with status 500, `PostLogRequest`, `EndRequest`, pre-send pair | `LogRequest` and `PostLogRequest` fire after **every** early end, so the early-end jump target must land on `LogRequest`, not past it. The port matches the order but reports `Response.StatusCode` as 200 at `LogRequest` after an unhandled error, because its error page and status land after `EndRequest`; error-page timing is out of job 3's scope. |

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
| IV18 | Read `CurrentNotification`/`IsPostNotification` outside the IV6 events on the integrated pool (one scratch site on `winbox` IIS 10, integrated v4.0 pool, taken 2026-09-04): `Application_Start` (the sender is `HttpApplicationFactory`, both `HttpContext.Current` and the application's `Context`), module `Init` on the first (special) instance and on a request instance, a page, `Application_Error` after a page throw, `HttpContext.AddOnRequestCompleted`, and a `-=` from `BeginRequest` | `Application_Start` and the special instance's module `Init`: `NullReferenceException` from both reads. Module `Init` on a request instance: `BeginRequest`/false. Page: `ExecuteRequestHandler`/false. `Application_Error`: `ExecuteRequestHandler`/false. `RequestCompleted`: `EndRequest`/false. The `-=`: `InvalidOperationException` with IV10's message | Before any notification exists Framework itself throws; a request instance built mid-request is inside `BeginRequest`; `RequestCompleted` still reads `EndRequest`; unbinding is gated like binding. |
| IV19 | `Request.Headers.Set("X-Probe")` and `Remove("Referer")` from a page with the server-variable collection in three states, integrated (same rig as IV18): created but unpopulated (a `REQUEST_METHOD` read first, one of the `GetSimpleServerVar` shortcuts); populated first (`Count` read); not created before the mutation | Unpopulated: `HTTP_X_PROBE` = `p` but `HTTP_REFERER` still the original. Populated: `HTTP_REFERER` null. Not created: `HTTP_REFERER` still the original | A `Set` survives population; a `Remove` made before population is lost to it. Same shape as the port's `_unsyncedEntries` path, so no code change. |
| IV20 | Module `Dispose` does `app.EndRequest -= handler` at app-domain unload (`HttpRuntime.UnloadAppDomain()` from a page), integrated, logged to a file | Request instances: `InvalidOperationException` with IV10's message; `Dispose` runs to its end and `Application_End` still fires. The special instance's `Dispose` unbinds without error | `DisposeInternal` swallows the refusal, as its code reads; the port shares that code. |
| IV14 | `POST` 8 KB to a folder with `maxRequestLength="4"` (KB), no `Connection: close`, both pools | Both return 500 with the same ASP.NET error body. Integrated: `Content-Length: 4802`, no `Connection` header, **socket stays open** (client read timed out waiting). Classic: `Connection: close`, **no `Content-Length`**, server closes the socket immediately. 1 KB control returns 200 keep-alive on both | The classic `CloseConnectionAfterError()` arm is observable on the wire as a connection reset and a missing `Content-Length`. |

### The remainder

Captured 2026-09-04 on `winbox`, IIS 10.0 / Windows 11, .NET Framework System.Web 4.8.9344.0
(NET481REL1LAST_25H2_B), on a second rig of the same shape: one application directory served by
an **Integrated** v4.0 pool on port 8123 and a **Classic** v4.0 pool on port 8124, `Global.asax`
tracing every managed event to a file, a `ProbeModule` covering module `Init` and the
authentication events, and `probe.aspx` dispatching stimuli on the query string. Raw sockets on
the box. Anonymous authentication, `customErrors mode="Off"`, `debug="false"`,
`authentication mode="None"` unless a row says otherwise.

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| IV21 | `?e=throw&w=1`: page throws; `Response.Write` at `LogRequest`, `PostLogRequest`, `EndRequest`. Variant `&clear=log` calls `Response.Clear()` at `LogRequest` first | Integrated: status is 200 at `Application_Error`, **500 from `LogRequest` onward**; the wire carries the full ASP.NET error page followed by `[LOG][PLOG][END]`; with `clear=log` the body is exactly `[LOG][PLOG][END]`, `Content-Length: 16`, status still 500. Classic: no Log events; status is **200 at `EndRequest`**; the `[END]` write reports success but never reaches the wire — the body is the error page alone (5168 bytes vs 5184) | Integrated renders the error page and sets the status before `LogRequest`, so the Log and `EndRequest` handlers see a complete 500 response they can append to or clear. Classic renders after `EndRequest` and discards whatever those handlers wrote. |
| IV22 | Read `HttpContext.Current.Request`/`.Response` and the `HttpApplication.Request`/`.Response` properties at `Application_Start`, the `HttpApplication.Init()` override, and `IHttpModule.Init` | Integrated — `Application_Start`: `HttpContext.Current` present, `Current.Request`/`Current.Response` throw `HttpException` "Request is not available in this context" / "Response is not available in this context."; `HttpApplication.Init()`: same; module `Init` on the **first** instance: `Current.Request.Path` = `/`, `Current.Response` live, `CurrentNotification` throws `NullReferenceException`; module `Init` on a **request** instance: both throw, `CurrentNotification` = `BeginRequest`. Classic — `Application_Start`, `HttpApplication.Init()` and module `Init` all read `Current.Request.Path` = `/probe.aspx` and a live `Current.Response`. `HttpApplication.Request`/`.Response` throw the same `HttpException` on **both** pools in every one of these places | `HideRequestResponse` is integrated-only and covers `Application_Start`, the application's own `Init()`, and module `Init` for request instances — but not the first instance's module `Init`, which sees a synthetic `/` request. The `HttpApplication` properties are hidden in both modes. |
| IV23 | `Request.ServerVariables["WEBSOCKET_VERSION"]` on a plain non-upgrade `GET` | Integrated: `13`. Classic: `null`. Neither pool lists the name in `ServerVariables.AllKeys`; `Count` is 45 on both. `HttpContext.IsWebSocketRequest` is `False` on integrated and throws `PlatformNotSupportedException` on classic | The variable answers from the server-variable shortcut path, not from the enumerated collection. Katana's capability probe reads exactly this name. |
| IV24 | Read `Response.ClientDisconnectedToken` in a page and from `HttpContext.AddOnRequestCompleted`; then `?e=hold`: flush, then poll the token and `Response.IsClientConnected` for 20s while the client closes the socket at 1.2s (FIN and RST), with and without a periodic 1-byte write | Integrated: `CanBeCanceled=True`, `IsCancellationRequested=False` in the page **and** still readable with the same values from `AddOnRequestCompleted`. Client closes with no server write, FIN or RST: neither the token nor `IsClientConnected` changes in 20s. With a write+flush every 2s: both flip together at 2291ms — the first write after the close — and the flush itself did not throw. Classic: `PlatformNotSupportedException` "This operation requires IIS version 7.5 or higher running in integrated pipeline mode." from every read | The token exists for the whole request and past its end, but IIS only learns about the disconnect when the server touches the connection; an idle request is never signalled. This is Framework's documented "we do not guarantee that we will ever transition the token to a canceled state". |
| IV25 | `?e=abort`: write, flush, `Request.Abort()`, then keep running | Integrated: `Abort()` returns, the code after it runs, `Response.IsClientConnected` is `False`, a further `Response.Write` succeeds, and the next `Response.Flush()` throws `HttpException: The remote host closed the connection. The error code is 0x80070057.` The client received `BEFORE-ABORT` and then a forcible reset (`IOException`, connection forcibly closed). Classic: `PlatformNotSupportedException` "This operation requires IIS integrated pipeline mode."; the request finishes normally and the client receives `AFTER-ABORT` | `Abort` resets the TCP connection without ending the request: managed code keeps running and only a flush surfaces the loss. |
| IV26 | `?e=sub`: read `Response.SubStatusCode`, set it to 7, read back; variant also sets `StatusCode = 404` then `SubStatusCode = 8` | Integrated: default `0`, set succeeds, reads back `7`; the wire is unchanged — `200 OK` with the page body, and `404 Not Found` in the variant. Classic: `PlatformNotSupportedException` "This operation requires IIS integrated pipeline mode." from get and set alike | The substatus is a stored value with no effect on the response bytes; only the IIS log consumes it. |
| IV27 | Trace `Context.User` at `AuthenticateRequest` and `PostAuthenticateRequest` from both a module and `Global.asax`, under `authentication mode` `None`, `Forms` and `Windows`, both pools | `mode="None"` and `mode="Forms"`: **identical on both pools** — `null` at `AuthenticateRequest`, `GenericIdentity` (empty name, `IsAuthenticated=False`) at `PostAuthenticateRequest`. `mode="Windows"`: integrated `null` at `AuthenticateRequest` and `WindowsIdentity` at `PostAuthenticateRequest`; classic **`WindowsIdentity` already at `AuthenticateRequest`**. `HttpApplication.DefaultAuthentication` is an `internal` event: `Global.asax` cannot subscribe (`CS1061` at compile), and an `Application_DefaultAuthentication` method never fires on either pool | The `DefaultAuthenticationModule` hook position is observable only through `Context.User` timing, and only under `mode="Windows"`. With the principal supplied by that module, the classic `DefaultAuthentication` step sits between the two events and produces the integrated timing anyway. |
| IV28 | `?e=ieb` on a `POST`: read `Request.Form`, then call `Request.InsertEntityBody()` and `InsertEntityBody(buffer, 0, 3)` | Integrated: both overloads return, no exception, and the response is unchanged (`200`, page body). Classic: `PlatformNotSupportedException` "This operation requires IIS integrated pipeline mode." from both | With no native handler following, the integrated call is observably a no-op. |
| IV29 | A folder carrying `system.webServer/security/authorization` `<add accessType="Deny" users="*" />`, and a second folder carrying `<handlers accessPolicy="Read" />`, each holding an `.aspx`; also a non-existent `.txt` in the first | Deny: **401.2** from module `UrlAuthorizationModule`, error code `0x80070005`, on **both** pools, for the `.aspx` and the `.txt` alike. `accessPolicy="Read"`: **403.1** from `IIS Web Core`, `0x80070005`, on **both** pools. The page never ran in any case | Both native denial paths are enforced by IIS ahead of managed code and are identical in the two pipeline modes, so neither is an integrated-versus-classic divergence. `CallHandlerExecutionStep`'s `IsHandlerExecutionDenied` 403 is not what a denied request meets. |
| IV30 | Register a `HostingEnvironment.StopListening` handler and an `IRegisteredObject` that is also `IStopListeningRegisteredObject` at `Application_Start`; warm the application, then `appcmd stop apppool` | Integrated: `HostingEnvironment.StopListening` and `IStopListeningRegisteredObject.StopListening` fire together, then `IRegisteredObject.Stop(immediate: false)` ~1.0s later, then `Stop(immediate: true)` ~30s after that, then `Application_End` with `ShutdownReason.HostingEnvironment`. Classic: neither StopListening signal ever fires; only `Stop(false)` → 30s → `Stop(true)` → `Application_End`. An app-domain recycle (`Global.asax` edit) raises neither signal on either pool | The early stop signal precedes `IRegisteredObject.Stop` by about a second and exists only on integrated. It is what Katana's `ShutdownDetector` binds to so a long-running request is cancelled before the drain deadline. |

The rig (two sites, one shared application directory, raw-socket reader) was removed after the
readings; nothing durable was left on the host.
