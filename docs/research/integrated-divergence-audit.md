# Managed integrated-vs-classic divergence audit

The port runs the classic managed pipeline ([ADR 0001](../adr/0001-runtime-compatibility-model.md)),
but the migrating audience ran IIS integrated mode: every integrated-mode branch in the
imported source is behavior they observed and this runtime never executes. This audit
enumerates those branches so each one ends as take-integrated-branch, shim, or a fail-fast
diagnostic naming the boundary — never a silent classic-mode difference
([backlog](../backlog.md)).

Research only. Current support lives in the [compatibility map](../compatibility.md);
priority lives in the backlog.

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

Classification:

- **PLUMBING** — not application-visible: native buffer/notification handoffs, perf
  counters, trace/ETW glue, debug assertions.
- **RESOLVED** — application-visible and already handled; the fence, ledger id, ADR or
  compatibility row is cited.
- **APP-VISIBLE OPEN** — application-visible and unresolved.

## Counts

| Classification | `UseIntegratedPipeline` sites | Other integrated gates (members) |
| --- | --- | --- |
| PLUMBING | 31 | 17 |
| RESOLVED | 10 | 10 |
| APP-VISIBLE OPEN | 34 | 7 |
| Total | 75 | 34 |

The 34 open `UseIntegratedPipeline` sites collapse to 17 distinct behaviors: the
`MapRequestHandler`/`LogRequest`/`PostLogRequest` event family alone accounts for 12 of them,
the `HideRequestResponse` pairs for 4, and the `HostingEnvironment` throttle accessors for
another 4.

## `HttpApplication.cs` — 34 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `IsContainerInitalizationAllowed` | True while re-running `InitInternal` after IIS event registration | PLUMBING | Module step containers exist only for native notification dispatch |
| `ThrowIfEventBindingDisallowed` | `InvalidOperationException` when an event is bound after both inits complete | **OPEN** | Port accepts the binding and `CreateEventExecutionSteps` has already snapshotted handlers, so it silently never runs |
| `FindISessionStateModule` | Returns the session module so `EnsureReleaseState` can release before a child request | PLUMBING | Child `Server.Execute` shares the parent's session; the release exists for `TransferRequest`, which is unsupported (P62) |
| `AcquireNotifcationContextLock` / `ReleaseNotifcationContextLock` (2 sites) | `Debug.Assert` | PLUMBING | Debug-only; callers are integrated-only |
| `AsyncResult` get/set (2 sites) | Stores the pending result on `NotificationContext` instead of `_ar` | PLUMBING | Per-notification storage; classic uses the field |
| `MapRequestHandler` event add/remove (2 sites) | Event subscribable, fires between `PostResolveRequestCache` and handler creation | **OPEN** | `PlatformNotSupportedException`; classic `BuildSteps` has no such step |
| `LogRequest` event add/remove (2 sites) | Fires after `EndRequest` | **OPEN** | Same |
| `PostLogRequest` event add/remove (2 sites) | Fires after `LogRequest` | **OPEN** | Same |
| `AddOnMapRequestHandlerAsync` (2 overloads) | Async form of the above | **OPEN** | Same |
| `AddOnLogRequestAsync` (2 overloads) | Async form | **OPEN** | Same |
| `AddOnPostLogRequestAsync` (2 overloads) | Async form | **OPEN** | Same |
| `ProcessSpecialRequest` enter/exit (2 sites) | Sets `HttpContext.HideRequestResponse`, so `Context.Request`/`Response` throw inside `Application_Start` and friends | **OPEN** | Port leaves them reachable — more permissive than what the audience observed |
| `InitInternal` module build | `InitIntegratedModules` from the merged `system.webServer/modules` list | RESOLVED | Port's `InitModules` is fenced to build the same integrated collection (P83) |
| `InitInternal` `HideRequestResponse` around `Init()` (2 sites) | Request/response hidden during `Init()` and module init | **OPEN** | Same family as `ProcessSpecialRequest` |
| `InitInternal` step manager | `PipelineStepManager` | RESOLVED | Classic `ApplicationStepManager` is the decision ([ADR 0001](../adr/0001-runtime-compatibility-model.md)) |
| `DisposeInternal` module key | Tracks `_currentModuleCollectionKey` so a module can unregister during `Dispose` | PLUMBING | Event maps exist only in integrated |
| `HookupEventHandlersForApplicationAndModules` | A failing `add_XXX` rethrows; classic swallows it | **OPEN** | Combined with the rows above: a `Global.asax` `Application_LogRequest`/`Application_MapRequestHandler` throws `PlatformNotSupportedException` into a bare `catch` and is silently dropped |
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
| `CurrentNotification` get/set (2 sites) | Returns the executing `RequestNotification` | **OPEN** | `PlatformNotSupportedException` |
| `IsPostNotification` get/set (2 sites) | Returns whether this is the post-phase | **OPEN** | `PlatformNotSupportedException` |

## `HttpRuntime.cs` — 4 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `UsingIntegratedPipeline` (public) | `true` under an integrated pool | **OPEN** | The port models an integrated pool for modules (P83), handlers (P85) and request filtering (P86) but answers `false` here, so third-party feature detection silently takes classic branches |
| `UseIntegratedPipeline` (internal getter) | Definition site | PLUMBING | |
| `Dispose` drain | `PipelineRuntime.WaitForRequestsToDrain()` vs active-count spin | PLUMBING | Process replacement owns shutdown ([ADR 0012](../adr/0012-runtime-initiated-restart.md)) |
| `ProcessRequest(HttpWorkerRequest)` | Refuses the classic entry point | RESOLVED | Inverse gate; this entry point is the port's engine ([ADR 0001](../adr/0001-runtime-compatibility-model.md)) |

## `HttpResponse.cs` — 2 sites

| Member | Integrated behavior | Class | Note |
| --- | --- | --- | --- |
| `Redirect` (VSO 360276) | Sets `ContentType = "text/html"` before writing the Object-moved body | **OPEN** | The classic default content type is already `text/html`, so the wire bytes probably match; unmeasured |
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
| `DefaultAuthenticationModule.Init` | Hooks `PostAuthenticateRequest`; classic hooks `DefaultAuthentication` | **OPEN** | The default principal is established at a different point relative to other `PostAuthenticateRequest` subscribers (notably `RoleManagerModule`) |

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
| `HttpRequest.EnsureHeaders` | Collection is writable and writes through to the native header block | **OPEN** | Port calls `MakeReadOnly()`, so `Request.Headers.Set/Add/Remove` throws where the audience mutated request headers freely |
| `HttpRequest.GetServerVars` | Collection is writable | RESOLVED | `ServerVariables.Set` unavailable, stated in P76 and the compatibility row |
| `HttpResponse.Headers` | Native header block backs the collection | RESOLVED | P68, readings H1–H16; fenced so the managed collection is the store |
| `HttpResponse.SubStatusCode` get/set | IIS substatus for the error code | **OPEN** | `PlatformNotSupportedException`; the port has no substatus channel (hidden-segment 404.8 is already noted as app-shaped) |
| `HttpResponse.AddOnSendingHeaders` | Per-request pre-send callback | **OPEN** | `PlatformNotSupportedException`. `OwinCallContext.RegisterForOnSendingHeaders` probes it reflectively and swallows the failure, so Katana loses its non-OWIN flush notification |
| `HttpResponse.ClientDisconnectedToken` | Cancellation token signalled on client disconnect (IIS 7.5+) | **OPEN** | `PlatformNotSupportedException`; long-poll/SignalR-shaped code uses it. `Owin.DisconnectWatcher` gates on `IISVersion` + `UsingIntegratedPipeline` and falls back |
| `HttpRequest.Abort` | Forcibly resets the TCP connection | **OPEN** | `PlatformNotSupportedException`; Kestrel exposes an abort feature |
| `HttpRequest.InsertEntityBody` (2 overloads) | Hands the read entity back to IIS | **OPEN** (very low) | No native handler follows; the honest answer is a boundary-naming refusal or a no-op |
| `HttpRequest.HttpChannelBinding` | Extended-protection binding token | RESOLVED | Windows authentication is unsupported |
| `HttpRequest.TlsTokenBindingInfo` | Token-binding info on Win10+ | RESOLVED | The imported contract is "null when unavailable"; the port returns null |
| `HttpRequest.ContentLength` limit, `HttpBufferlessInputStream.ValidateRequestEntityLength` | Integrated does **not** close the connection on an oversize body; classic does | **OPEN** (low) | The port closes; the audience saw the connection stay open behind the 500 |
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
| `HttpResponse.Filter` setter, `FilterOutput`, `GetSnapshot`, `UpdateNativeResponse`, `ClearNativeResponse`, `Clear`, `EndFlush`, `Flush`, `WriteSubstBlock`, `GetHttpHeaderContentEncoding` | Native response manipulation | PLUMBING | `GetHttpHeaderContentEncoding` is already fenced for the managed collection |
| `HttpResponse.AppendToLog` | Routes to `Request.AppendToLogQueryString` | PLUMBING | No IIS log |
| `Handlers/TransferRequestHandler.ProcessRequestAsync` | Schedules a child `ExecuteUrl` for extensionless URLs | RESOLVED | The baseline row is transparent to dispatch (P85, `IisHandlerRoutes.IsTransferRequest`) |
| `Handlers/AssemblyResourceLoader.EnsureHandlerExistenceChecked` | Asks IIS to map `WebResource.axd` | RESOLVED | Fenced `#else` branch |
| `Handlers/TraceHandler.ProcessRequest` | Sets the content type explicitly | PLUMBING | Classic default is the same `text/html` |
| `httpserverutility.Execute` handler mapping | `MapIntegratedHttpHandler` | RESOLVED | The port's `MapHttpHandler` is fenced to the integrated list (P85) |
| `UI/Page.ProcessRequest` (2 sites) | `needToCallEndTrace` for IIS trace events | PLUMBING | ETW inert (P04) |
| `EtwTrace.Trace`, `RootedObjects.WorkerRequest` | Native trace/rooting | PLUMBING | P04 |

## APP-VISIBLE OPEN, ranked by likelihood a migrating application hits it

1. **`Global.asax` / module subscriptions to `MapRequestHandler`, `LogRequest`,
   `PostLogRequest`** (`HttpApplication`, 14 sites plus
   `HookupEventHandlersForApplicationAndModules`). Integrated fired these events; classic has
   no such steps and the `add_` accessors throw. A module's explicit `+=` surfaces as a
   `PlatformNotSupportedException` at application init (loud, but the message tells the
   operator to change IIS pipeline mode, which no configuration of this host can do). A
   `Global.asax` `Application_LogRequest` is worse: the reflection hookup's bare `catch`
   swallows the exception, so the handler is silently dropped — the exact failure mode the
   doctrine forbids. **Disposition: take the integrated branch.** `LogRequest`/`PostLogRequest`
   append cleanly after `EndRequest` in `ApplicationStepManager.BuildSteps`;
   `MapRequestHandler` goes before `MapHandlerExecutionStep`. Until then the swallow must at
   minimum become a fail-fast naming the event. **Reading:** yes — pin, on winbox, where
   `LogRequest`/`PostLogRequest` fall relative to `EndRequest` and to response completion, and
   whether a `MapRequestHandler` subscriber can change `Context.Handler`. **Effort:** story
   (three steps plus the diagnostic), with the swallow fix as a separate lines-of-fence change.

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
   integrated and then hit the refusals. **Reading:** no. **Effort:** ADR + lines-of-fence.

3. **`HttpContext.CurrentNotification` / `IsPostNotification`** (`HttpContext`, 4 sites).
   Modules written for integrated read these to know where they are; the port throws
   `PlatformNotSupportedException` from a property read, which is rarely guarded.
   **Disposition: shim.** `ApplicationStepManager` already knows which event's steps it is
   executing (it tracks `_beginRequestStepEndIndex` for the WebSocket fence), so a
   step→`RequestNotification` map gives honest values for every classic event, with the
   integrated-only notifications never reported. **Reading:** no — the mapping is the event
   names. **Effort:** new seam (small).

4. **Writable `Request.Headers`** (`HttpRequest.EnsureHeaders`). Integrated let an
   application or module mutate request headers and have downstream code see the change; the
   port calls `MakeReadOnly()`, so `Set`/`Add`/`Remove` throw. Common in SSO, header-based
   impersonation and reverse-proxy shims. **Disposition: shim**, the mirror of P68 — without a
   native block the managed collection is simply the store, and `SynchronizeHeader` already
   knows how to keep `ServerVariables` in step. **Reading:** worth one — confirm on winbox
   whether a mutated request header is visible to `Request.ServerVariables["HTTP_*"]`,
   `Request.UserAgent`-style typed accessors and a later module. **Effort:** lines-of-fence
   plus a scenario.

5. **`HttpApplication.OnExecuteRequestStep`** (`HttpApplication`). Step-wrapping is how
   request-scoped instrumentation (APM agents, diagnostic middleware) attaches. The port
   throws, yet `ExecuteStepImpl` already invokes `_stepInvoker` in both step managers — only the
   gate blocks it. **Disposition: take the integrated branch.** **Reading:** no. **Effort:**
   lines-of-fence.

Remaining open items, in descending likelihood: `Response.AddOnSendingHeaders` (Katana probes
it and silently degrades); `Response.ClientDisconnectedToken` and `Request.Abort` (long-poll
and streaming apps; Kestrel has both capabilities, so both are shim candidates);
`HideRequestResponse` during `Application_Start`/`Init` (port is more permissive — take the
integrated branch for least astonishment); `DefaultAuthenticationModule` hooking
`DefaultAuthentication` instead of `PostAuthenticateRequest` (principal-establishment ordering);
`ThrowIfEventBindingDisallowed` (late event binding is silently inert — should fail fast);
`Response.SubStatusCode`; oversize-body connection close; `CallHandlerExecutionStep`'s
`IsHandlerExecutionDenied` 403; `HostingEnvironment.MaxConcurrent*PerCPU` (4 sites, one
behavior — keep the refusal, reword it); `Response.Redirect` content type;
`Request.InsertEntityBody`; `Response.PushPromise` (silent no-op, needs only a compatibility
row); `DefaultAuthentication.Authenticate` and `DefaultHttpHandler` (inverse gates the audience
never exercised).

## Recommended winbox readings

1. `LogRequest`/`PostLogRequest` ordering: a module logging at `EndRequest`, `LogRequest`,
   `PostLogRequest` and `RequestCompleted`, against a page that flushes early — pins whether
   the response has left the wire before `LogRequest` runs.
2. `MapRequestHandler` capability: does a subscriber that assigns `Context.Handler` or calls
   `RemapHandler` in `MapRequestHandler` win over the `<handlers>` selection?
3. Request-header mutation: set and remove a request header at `BeginRequest`, then read
   `Request.Headers`, `Request.ServerVariables["HTTP_*"]`, the typed accessor, and the value a
   later module sees.
4. `Response.Redirect` wire bytes on integrated: full head of a `Redirect("/x")` from a page and
   from a module, to settle the `ContentType = "text/html"` line.
5. Oversize request body: exact status, body and connection disposition when `ContentLength`
   exceeds `maxRequestLength` on integrated versus classic.
6. Default principal ordering: with `DefaultAuthenticationModule` and a probe module both on
   `PostAuthenticateRequest`, which observes the anonymous principal first, and where
   `RoleManagerModule` lands.
