# Managed Web Forms runtime port plan

Status: accepted target architecture.

This is the canonical plan for porting the managed Web Forms runtime. It
supersedes the direction in
[`http-runtime-porting-plan.md`](http-runtime-porting-plan.md).

Supporting records:

- [runtime model](classic-managed-runtime-model.md);
- [portability ledger](portability-ledger.md);
- [architecture decisions](adr/);
- [semantic vocabulary](../CONTEXT.md).

## Goal and compatibility envelope

Port the managed classic ASP.NET request lifecycle, not IIS:

```text
application activation
→ HostingEnvironment / HttpRuntime initialization
→ first-request initialization
→ HttpApplication pipeline
→ configured module and handler execution
→ System.Web response completion
```

The first supported profile is the classic managed pipeline entered through
`HttpRuntime.ProcessRequest(HttpWorkerRequest)`.

Initial exclusions:

- IIS integrated notifications and native module inventory;
- `w3wp.exe`, `webengine4.dll`, ISAPI, and IIS configuration tokens;
- secondary AppDomains, remoting, in-process application replacement;
- partial trust and legacy CAS;
- configuration/file-change reload;
- dynamic `.aspx`, `App_Code`, and `Global.asax` until their later slices;
- request bodies and client-visible response streaming in the first slice.

One immutable application generation runs per process. External process
replacement provides isolation and restart.

## Compatibility method

Use these authorities in order:

1. .NET Framework 4.8.1 executable behavior;
2. pinned Reference Source for call structure and intent;
3. official documentation for public contracts;
4. the sibling Portable.System.Web POC only as hazard/design evidence.

Do not assume current documentation or port code is correct. Preserve a legacy
sequence only after identifying its postconditions. Every platform dependency
receives one treatment:

1. disable it through normal Framework configuration;
2. retain its existing capability-inactive behavior;
3. replace only its nonportable leaf while preserving callers and postconditions;
4. fail explicitly as unsupported.

Never compile out a lifecycle phase merely because its original leaf is native.

## Target ownership

The host-neutral public shape is intentionally small:

```csharp
var application = WebFormsApplication.Create(options);
await application.ProcessRequestAsync(workerRequest, cancellationToken);
await application.StopAsync(cancellationToken); // implemented in lifecycle slice
```

Names remain provisional. Contract:

- `Create` validates immutable host-owned inputs without mutating System.Web.
- The first routed request single-flights activation.
- Activation enters `ApplicationManager.CreateObjectInternal` with an internal
  registered lifecycle bridge.
- `ApplicationManager` and `HostingEnvironment` remain internal compatibility
  machinery.
- Request dispatch enters public
  `HttpRuntime.ProcessRequest(HttpWorkerRequest)` directly.
- The Kestrel adapter translates, awaits, and commits; it does not own
  System.Web lifecycle.

Lifecycle:

```text
Registered → Activating → Accepting → StopRequested → Stopped
```

Concurrent first requests await the same activation. Existing System.Web
first-request locking remains authoritative after activation.

## Configuration and deployment contract

Required explicit inputs:

- application ID;
- absolute physical and virtual application roots;
- absolute writable application work root;
- machine and root-web configuration assets.

Rules:

- Validate host-owned inputs before global mutation.
- Do not pre-open or eagerly evaluate application System.Web configuration.
- Retained `HostingEnvironment` and `HttpRuntime.HostingInit` own configuration
  installation, parsing, inheritance, caching, and error timing.
- Derive root `web.config` structurally from pinned .NET Framework 4.8.1.
- Inventory every assembly-identity or portability delta.
- Disable FCN and related ACL reads through hosting/configuration settings.
- Treat config, binaries, pages, resources, and generated outputs as immutable
  for the process generation.
- Preserve `SetUpCodegenDirectory`; replace only its AppDomain/temp leaf with a
  deterministic directory beneath the work root.
- Resolve managed application assemblies from explicit `<application>/bin`
  through one immutable default-`AssemblyLoadContext` fallback.
- Do not use host project references, current-directory probing, runtime
  assembly location, or ambient temporary directories.

## Slice 0: semantic evidence

Deliver:

- one net481 oracle harness using a recording `HttpWorkerRequest`;
- provenance-stamped generated oracle traces;
- a narrow checked-in normalization manifest;
- Windows regeneration/verification;
- portable trace comparison on every supported OS;
- the first classic-path rows in the portability ledger;
- a pinned Framework root-configuration artifact and delta inventory.

Compare ordered lifecycle events, response status/headers/body, exception
ownership, and completion. Use IIS classic probes only when the managed harness
cannot reproduce a behavior.

Exit gate:

- oracle traces are generated, not hand-authored;
- normalization has no broad scrubbing;
- every mismatch is fixed, documented as a deviation, or unsupported.

## Slice 1: bodyless precompiled handler

### 1A. Reshape initial bootstrap

Starting from `fcc0372`:

Retain:

- option/path normalization;
- explicit configuration mapping;
- reversible current-AppDomain slot binding;
- useful validation and tests.

Replace:

- public static `Initialize` with the process-scoped application owner;
- broad mapped-configuration preflight with host-contract validation;
- catch-all `Faulted` policy with ownership-based failure handling;
- minimal invented root defaults with a Framework-derived baseline.

Add:

- explicit work root;
- application-bin assembly catalog/resolver;
- five-state lifecycle and single-flight activation.

### 1B. Activate through retained hosting

Activation must:

1. validate the immutable resolver/binding plan;
2. publish the application-bin resolver;
3. bind the five Framework AppDomain data slots atomically from the owner's
   perspective;
4. call `ApplicationManager.CreateObjectInternal`;
5. enter the current-AppDomain creation leaf;
6. execute normal `HostingEnvironment.Initialize`;
7. preserve normal hosting flags, except explicit FCN disable/ACL-skip values;
8. create/register the lifecycle bridge;
9. publish `Accepting` only after all request-independent postconditions hold.

Do not set `HideFromAppManager`, `ThrowHostingInitErrors`,
`DontCallAppInitialize`, or integrated-pipeline state.

### 1C. Port the classic runtime path

Follow the ledger through:

```text
HttpRuntime.StaticInit / Init
→ HostingInit
→ BuildManager.InitializeBuildManager
→ FirstRequestInit
→ HttpApplicationFactory
→ HttpApplication
→ configured IHttpModule
→ configured IHttpHandler
→ FinishRequest / EndOfRequest
```

Keep `BuildManager` initialization even though source compilation is deferred.
Dynamic compilation fails only at its specific leaf.

The fixture clears inherited handler/module collections, then configures:

- one event-recording managed module;
- one precompiled handler type from an unreferenced assembly in fixture `bin`.

Classic `HttpModulesSection.CreateModules` still appends its implicit
`DefaultAuthenticationModule`; retain and probe that behavior.

Handler selection must use normal `system.web/httpHandlers` matching and
factory/type-resolution code. The host must never inject the handler.

### 1D. Add the Kestrel adapter

First transport envelope:

- bodyless request;
- empty `PathInfo`;
- method, path, query, protocol, scheme, addresses, and standard headers;
- status, headers, memory response fragments, logical flush, final completion.

The adapter calls `HttpRuntime.ProcessRequest` directly; no `Task.Run`.

Response output uses a per-request memory/file spool. `EndOfRequest` seals
managed output. The adapter then commits it asynchronously to Kestrel. Keep
Kestrel synchronous I/O disabled.

Disconnect marks the worker request disconnected but never abandons a pipeline
that owns the request.

### 1E. Failure and shutdown contract

- System.Web-owned module/handler/startup errors remain inside normal error
  formatting and `EndRequest`.
- Only exceptions that actually escape `HttpRuntime.ProcessRequest` propagate
  through adapter completion, exactly once.
- If request entry escapes before pipeline completion, the worker request
  explicitly faults its completion.
- Cached `HostingInit`/`FirstRequestInit` errors receive System.Web's error
  response where request machinery exists.
- An imported AppDomain shutdown request emits one terminal host notification.
- The ASP.NET Core adapter calls `IHostApplicationLifetime.StopApplication`.
- Runtime code never calls `Environment.Exit` or retries initialization.

### Slice 1 exit gate

Required probes:

- cold synchronous success;
- concurrent cold requests with one activation/initialization;
- warm concurrent requests with isolated `HttpContext` and pooled applications;
- delayed async handler with awaited completion;
- module `CompleteRequest`, skipped handler, and `EndRequest`;
- module and handler exceptions owned by System.Web;
- handler-resolution/configuration failure;
- exactly one terminal shutdown notification;
- handler assembly loaded only from application `bin`;
- no reachable IIS/native operation on the supported path.

## Later vertical slices

| Slice | Scope | Principal gate |
| ---: | --- | --- |
| 2 | Dynamic compilation substrate, pre-app-start, `App_Code`, `Global.asax`, `Application_Start` | startup ordering, concurrency, and failure parity |
| 3 | `.aspx` GET: parser, `PageHandlerFactory`, page lifecycle, rendering | deterministic page trace and output |
| 4 | Request-body bridge, forms, postback, view state, uploads | sync/async entity-body parity without assumed Kestrel sync I/O |
| 5 | Session, cache, authentication, resources, routing, other built-ins | feature-specific configuration and request parity |
| 6 | Graceful drain/disposal and broader transport: streaming, file send, path info, certificates, WebSockets | lifecycle and transport-specific gates |

Do not begin a slice until its predecessor's differential gate passes.

## Review boundaries

Imported Reference Source edits require:

- a ledger row naming the blocked leaf;
- evidence that config/inactive behavior cannot satisfy the postcondition;
- retained Framework code under `NETFRAMEWORK` where useful;
- a focused unit test and relevant differential probe.

Host integration edits must not:

- initialize `HostingEnvironment` directly from middleware;
- select handlers;
- expose `ApplicationManager` publicly;
- use ASP.NET Core DI to replace System.Web object lifetimes implicitly;
- stop awaiting because a client disconnected.

## Initial-commit assessment

| Commit | Keep | Reshape |
| --- | --- | --- |
| `df49d38` | initialization phase evidence and postcondition discipline | integrated-pipeline recommendation, bootstrap-preflight assumption, blanket terminal failure |
| `fcc0372` | explicit inputs, normalization, mapping, binding, tests | static/eager lifecycle, broad config preflight, minimal root baseline, catch-all fault state |

Neither commit is discarded wholesale. Both are exploratory inputs, not
requirements or semantic authority.
