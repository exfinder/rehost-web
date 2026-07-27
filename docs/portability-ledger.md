# Classic-path portability ledger

Status: seed. Update only as an executable slice reaches each edge.

This is not a dependency encyclopedia or assumed truth. Each row records one
postcondition on the supported classic path. Reference Source explains the
edge; an oracle probe decides observable semantics.

Treatment:

- **keep** — unchanged managed behavior;
- **config-disable** — normal Framework configuration selects inactive behavior;
- **inactive** — existing initialized object naturally has no capability;
- **portable leaf** — retain callers/order; replace only the platform operation;
- **unsupported** — fail explicitly when selected;
- **deferred** — later compatibility slice.

State:

- **decision** — architecture selected, implementation/probe pending;
- **research** — exact treatment still requires evidence;
- **reached** — implemented and exercised by a running slice on at least one
  supported OS; remaining OS jobs still outstanding;
- **verified** — implementation and required probe pass.

## Activation and hosting

| ID | Edge and required postcondition | Legacy/platform dependency | Treatment | Proof | State |
| --- | --- | --- | --- | --- | --- |
| A01 | registration normalizes immutable identity and roots without global mutation | IIS/application ambient identity | portable leaf | invalid/corrected registration tests | decision |
| A02 | application `bin` can resolve an unreferenced managed handler assembly | AppDomain application base/private-bin probing | portable leaf | handler exists only in fixture `bin` | decision |
| A03 | `.appDomain`, `.appId`, `.appPath`, `.appVPath`, `.domainId` publish as one usable binding | child-AppDomain data | portable leaf | partial binding injection; no consumer sees it | decision |
| A04 | `ApplicationManager` per-app lock/context and object creation execute | default-domain manager/remoting | keep | activation trace includes manager/cache path | decision |
| A05 | application environment exists without child AppDomain | `AppDomain.CreateDomain`, setup, remoting | portable leaf | one current-domain environment; secondary-domain API rejects | decision |
| A06 | `HostingEnvironment.Initialize` receives explicit host/map/config inputs | IIS app host/config token | portable leaf | mapped configuration and path probe | decision |
| A07 | lifecycle object is registered and receives `Stop`, and shutdown completes deterministically | cross-AppDomain registered object; AppDomain unload as the shutdown-completion signal | portable leaf | exactly-once shutdown probe; harness exits 0 in ~0.5s | reached |
| A08 | normal classic hosting flags remain active | POC shortcut flags | keep | trace flags; AppInitialize eligibility | decision |
| A09 | FCN and ACL-read capability are authoritatively inactive | Windows directory notifications/ACL reads | config-disable | inactive object/postcondition tests | decision |

## `HttpRuntime` construction and hosting initialization

| ID | Edge and required postcondition | Legacy/platform dependency | Treatment | Proof | State |
| --- | --- | --- | --- | --- | --- |
| H01 | static runtime construction is allowed on every supported OS | unconditional Win32 gate | portable leaf | static-init probe on each OS | decision |
| H02 | timeout manager and request/completion callbacks exist | managed | keep | private postcondition/failure tests | decision |
| H03 | worker identity field has deterministic supported meaning | Windows process identity | research | consumer trace and oracle comparison | research |
| H04 | performance-counter calls preserve control flow with inactive counters | native performance counters/web engine | inactive | null/inactive counter probes | decision |
| H05 | `FileChangesMonitor` object exists with FCN disabled | Windows FCN/registry defaults | config-disable | timestamp/path consumers still work | decision |
| H06 | `DataDirectory` and application-directory checks complete | AppDomain data and Windows ACL assumptions | portable leaf | path/access/error probes | research |
| H07 | directory/bin monitor phase preserves postconditions while inactive | Windows change notifications | config-disable | no watch handles; initialized state | decision |
| H08 | object-cache host initializes before configuration consumption | managed/static hosting state | keep | ordering trace | research |
| H09 | minimal cache/trust/security/compilation/hosting sections are read in Framework order | mapped System.Configuration | keep | config access/order/error trace | decision |
| H10 | codegen directory is deterministic, writable, and generation-specific | runtime install dir/AppDomain dynamic directory/ambient temp | portable leaf | work-root and conflict probes | decision |
| H11 | prefetch phase cannot reach native Windows API | native prefetch | config-disable or inactive | effective-config and reachability probe | research |
| H12 | runtime publishes full trust and rejects partial trust/CAS at normal consumption point | CAS policy/AppDomain permission set | unsupported | exact config diagnostic and timing | decision |
| H13 | fusion/private-bin postcondition loads application assemblies | AppDomain fusion/shadow-copy APIs | portable leaf | A02 plus dependency resolution probe | decision |
| H14 | URL metadata and global configuration completion retain order | managed configuration/cache | keep | ordering and lazy-section probe | research |
| H15 | process/thread-pool policy does not silently retune shared host state | ASP.NET process model/native policy | config-disable or portable leaf | before/after process-policy probe | research |
| H16 | autogen/machine-key phase produces deterministic supported state | legacy machine/process secrets | keep or later security leaf | key initialization probe | research |
| H17 | `BuildManager.InitializeBuildManager` executes | AppDomain/codegen assumptions below it | keep | initialization trace | decision |
| H18 | resource perf-counter phase leaves valid inactive state | native counters | inactive | no native reachability | research |
| H19 | pre-application methods and `App_Code.AppInitialize` retain order | generated compilation/loading | deferred | slice-2 oracle trace | decision |

## First request and classic pipeline

| ID | Edge and required postcondition | Legacy/platform dependency | Treatment | Proof | State |
| --- | --- | --- | --- | --- | --- |
| R01 | public `HttpRuntime.ProcessRequest` guards, counters, queue, and admission execute | none for classic managed entry | keep | entry trace; prove no `ProcessRequestNow` shortcut | decision |
| R02 | `HttpContext`, `HttpRequest`, and `HttpResponse` are created from one worker request | IIS worker-request details | portable leaf | core and Kestrel mapping probes | decision |
| R03 | `FirstRequestInit` executes once using first real context | lazy static state | keep | concurrent cold-request probe | decision |
| R04 | application enabled/offline behavior retains request error/status semantics | filesystem/application file | keep or portable leaf | oracle cases | research |
| R05 | request queue initializes without hidden IIS/process tuning | thread/process heuristics | research | concurrency/admission trace | research |
| R06 | health monitoring heartbeat has explicit inactive or supported state | timers/providers/event log | config-disable | no provider/timer reachability | research |
| R07 | tracing initializes from normal configuration | diagnostics/perf integration | keep with inactive leaves | trace-disabled and enabled probes | research |
| R08 | IIS folder ACL restriction phase cannot mutate platform ACLs | IIS/Windows ACL | inactive via hosting flag | phase postcondition/no ACL calls | decision |
| R09 | bin preload cannot become ambient recursive probing | impersonation/fusion | application-bin resolver | app-bin dependency probe | decision |
| R10 | encoder and request validator initialize at deterministic point | managed/configured types | keep | first-request ordering/failure trace | research |
| R11 | factory lazy initialization and `Global.asax` semantics remain | dynamic compilation/FCN | deferred | slice-2 oracle trace | decision |
| R12 | configured modules plus implicit `DefaultAuthenticationModule` are created per pooled `HttpApplication` and disposed with it | reflection/config | keep | inventory/pooling/lifetime trace | decision |
| R13 | configured handler mapping/factory selects precompiled handler | BuildManager/config type resolution | keep | mapped handler fixture | decision |
| R14 | normal `HttpApplication` pooling and per-request `HttpContext` isolation remain | managed pool/static current context | keep | warm concurrent probe | decision |
| R15 | module short-circuit still reaches `EndRequest` | event/callback pipeline | keep | `CompleteRequest` trace | decision |
| R16 | sync and delayed async handlers unwind through normal callbacks | `IAsyncResult` callbacks | keep | paired sync/async oracle traces | decision |

## Completion, failure, and shutdown

| ID | Edge and required postcondition | Legacy/platform dependency | Treatment | Proof | State |
| --- | --- | --- | --- | --- | --- |
| C01 | managed module/handler errors use System.Web formatting | worker-response implementation | keep | error response and event trace | decision |
| C02 | only exceptions escaping request entry fault adapter completion | host task bridge | portable leaf | handled-versus-escaped probe | decision |
| C03 | `EndOfRequest` completes exactly once | native completion callback | portable leaf | sync, async, failure race tests | decision |
| C04 | disconnect is observable but does not abandon pipeline ownership | IIS disconnect callback/Kestrel cancellation | portable leaf | disconnect-before-async-complete test | decision |
| C05 | first-slice response preserves logical headers/fragments/order | native response elements/synchronous Kestrel stream | portable leaf | spool and final commit tests | decision |
| C06 | large spooled output uses owned generation work storage and cleans up | unmanaged buffers/ambient temp | portable leaf | spill, failure, disposal tests | decision |
| C07 | cached initialization failure can produce System.Web error page | AppDomain restart timer | keep plus shutdown leaf | initialization-error oracle trace | decision |
| C08 | AppDomain shutdown request notifies owner once | AppDomain unload/recreate | portable leaf | concurrent/repeated shutdown test | decision |
| C09 | owner requests Kestrel stop; replacement remains external | IIS/WAS recycle | portable leaf | fake host-lifetime probe | decision |
| C10 | graceful drain, `Application_End`, disposal, and `Stopped` | AppDomain unload coordination | deferred | lifecycle-slice gate | decision |

## Request-ownership slice (portable activation to `ProcessRequest`)

Edges reached in order by `PortableParity.Host run` while transferring request
ownership to `HttpRuntime.ProcessRequest`. Exercised on macOS `arm64` and Windows
`x64`, both `net10.0`, with identical events, status, and empty stderr. Framework
behavior is retained under `NETFRAMEWORK`, which this repository never defines.

| ID | Reached edge | Legacy/platform dependency | Treatment | State |
| --- | --- | --- | --- | --- |
| P01 | `ApplicationManager` type initialization | `System.Security.Policy.StrongName` for legacy CAS full-trust assemblies | unreachable Framework-only code; the only consumer is the secondary-AppDomain block | reached |
| P02 | `Misc.ReportUnhandledException` | native Windows event-log reporter in `webengine4.dll` | portable leaf: one seam writes `FormatExceptionMessage` output to an `EventSource`. Threw *out of* catch handlers and masked every activation failure until fixed | reached |
| P03 | `SimpleApplicationHost` physical path | hardcoded `"\\"` separator | portable leaf: `Path.DirectorySeparatorChar`; removed the OS-conditional workaround it had forced into `CurrentAppDomainHosting` | reached |
| P04 | `HttpRuntime.StaticInit` engine load | `webengine4.dll` load, `InitializeLibrary`, `PerfCounterInitialize` | optional IIS integration split from required managed init; `IsEngineLoaded` is deterministically false on every OS. On Windows `net10.0` the probe already computed false | reached |
| P05 | `HttpRuntime.Init` platform gate | `Environment.OSVersion.Platform != Win32NT` throw | unconditional Win32 gate removed from the portable path; contradicts the cross-platform contract | reached |
| P06 | `HttpConfigurationSystem.EnsureInit` | cast to `WebConfigurationHost`; modern `System.Configuration` wraps hosts in `ImplicitMachineConfigHost` | field is written once and never read (proven by CS0169); retained for Framework only | reached |
| P07 | `ApplicationImpersonationContext` construction | `OpenThreadToken`/`SetThreadToken`/`RevertToSelf` | one seam at `GetCurrentToken`; with a zero application identity token the whole subsystem is inert. Explicit rejection of `<identity impersonate="true"/>` is deferred to the identity slice | reached |
| P08 | `SetUpCodegenDirectory` | `AppDomain.SetDynamicBase` / `DynamicDirectory` (null on modern .NET) | portable leaf: `_codegenDir = codegenBase`, derived from the configured `compilation/tempDirectory` and the application name. No generation segment stands in for the one the CLR supplied. **Deferral:** a segment only matters once dynamic compilation writes here, and choosing one is a dynamic-compilation decision, so H10 stays partially met — see [runtime codegen and loading](follow-ups/runtime-codegen-and-loading.md) | reached |
| P09 | config map path selection | `HostingPreferredMapPath` → `IISMapPath` → metabase/IIS Express probing | portable leaf: the hosting environment's map path is authoritative, since no web server participates. **Narrowing:** Framework delegates per path — hosting map for in-app paths, IIS for paths outside the app. Portable uses the hosting map for all paths; without a web server there is no authority for out-of-app paths anyway. Config files in subdirectories *inside* the application are unaffected, because `UserMapPath` walks the parent hierarchy | reached |
| P10 | `IISMapPath.GetInstance` | IIS/metabase configuration | unsupported: actionable `PlatformNotSupportedException`, proving the supported path never reaches IIS configuration | reached |
| P11 | `SystemInfo.GetNumProcessCPUs` | `GetSystemInfo`, `GetProcessAffinityMask` | portable leaf: `Environment.ProcessorCount` already reports affinity-limited processors | reached |
| P12 | `SRef` cache size sampling | CLR-internal `System.SizedReference` | inactive: `ApproximateSize` returns 0, so `CacheSizeMonitor` is permanently blind to cache size and never trims on that signal. Memory-pressure trimming still functions. No portable API measures an object graph's retained size; deferred to a cache-memory slice | reached |
| P13 | `FileUtil.DirectoryExists` | `GetFileAttributesEx` | portable leaf: `File.GetAttributes`, matching the file's existing portable branch | reached |
| P14 | `Util.HasWriteAccessToDirectory` | `GetCurrentThreadId` for a scratch file name | portable leaf: `Environment.CurrentManagedThreadId` | reached |
| P15 | `SetAutogenKeys` | `EcbCallISAPI(GetAutogenKeys)` machine-persisted key material | only the persisted lookup is guarded; portable takes Framework's own random-key fallback. **Deviation:** keys are process-scoped, so ViewState and forms-auth tickets do not survive restart and cannot be shared between processes. Explicit `<machineKey>` still applies. See [machine-key follow-up](follow-ups/machine-key-and-viewstate-bootstrap.md) | reached |
| P16 | `MultiTargetingUtil` target validation | registry lookups for installed .NET Framework and supported SKUs | portable leaf: the implemented surface is reported as `4.8.1`, so higher targets still fail with the original actionable error | reached |
| P17 | `AspNetMemoryMonitor` totals | `GlobalMemoryStatusEx` | portable leaf: `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`. `s_totalVirtual` is only consulted on 32-bit and stays unset | reached |
| P18 | `ConfiguredProcessMemoryLimit` | `aspnet_wp.exe`/`w3wp.exe` module probes and IIS server config | inactive: neither worker process can exist, so the physical-memory heuristic applies | reached |
| P19 | `SafeNativeMethods.GetCurrentProcessId` | `kernel32` | portable leaf at the declaration: `Environment.ProcessId`, covering all four call sites without call-site churn | reached |
| P20 | `VersionInfo.GetLoadedModuleFileName` | `GetModuleHandle`/`GetModuleFileName` | portable leaf: `Environment.ProcessPath` for the main module, null otherwise; `ExeName` path stripping no longer assumes `\` | reached |
| P21 | `LowPhysicalMemoryMonitor.GetCurrentPressure` | `GlobalMemoryStatusEx` memory-load percentage | portable leaf: derived from `GCMemoryInfo` load and total. **Accepted startup transient:** `MemoryLoadBytes` is only populated after the first collection, so `InitHistory` seeds the pressure history with zero. Framework returns zero the same way when the native call fails, and the history self-corrects on the first `Update`. No portable API reports machine memory load before the first GC | reached |
| P22 | `CompilationLock` | native instrumented named mutex in `webengine4.dll` plus a registry-supplied mutex name | portable leaf: `System.Threading.Mutex` with the session-local name, satisfying the file's own `ROTORTODO`. Draining/lock-status logic is unchanged | reached |
| P23 | `FileEnumerator`, `FindFileData`, `FileAttributesData` | `FindFirstFile`/`FindNextFile`/`FindClose` and `WIN32_FIND_DATA` | portable leaf: one `System.IO` seam over `DirectoryInfo.EnumerateFileSystemInfos` and `FileSystemInfo`. No 8.3 alternate names exist, so both name forms are the real name. **Platform semantic:** `FileAttributes.Hidden` covers dot-files on Unix but not on Windows, so `IsHidden` classifies a different file set per OS. Its sole consumer, `MapPathBasedVirtualPathProvider`, decides application *content*: batch compilation, `App_Code`/`App_Themes`/`App_Browsers`/`App_WebReferences`, and the precompile file copy. Unreached today. The attribute query is kept because filesystem convention is not the leaf's to invent, but the compilation slice must decide whether content selection uses a name-based exclusion list instead — the mechanism Framework already uses for `_vti_cnf`. Recorded in [portable filesystem and configuration-path semantics](follow-ups/portable-filesystem-and-config-path-semantics.md) | reached |
| P24 | `CompilationUtil.GetRecompilationHash` | `CodeDomProvider.GetCompilerInfo("cpp")` | inactive: the lookup exists only to skip the C++ provider, which does not exist outside .NET Framework | reached |
| P25 | `UserMapPath.GetPhysicalPathForPath` | hardcoded `'/'` → `'\'` when combining a subdirectory path onto a mapped physical directory | portable leaf: `Path.DirectorySeparatorChar`, byte-identical on Windows. Not reached by the harness — the fixture's only `web.config` sits at the application root, so the child-path branch never runs — but covered by `UserMapPathTests`, which maps `/child/grandchild` through a `WebConfigurationFileMap` and fails against the original `'\\'` on non-Windows, where it produced a single filename containing literal backslashes. Siblings in `TemplateParser` and `SimpleWorkerRequest` remain for later slices; those in `MetabaseServerConfig` and `ProcessHostMapPath` are IIS-only and unreachable | reached |
| P26 | `HttpRuntime.ReleaseResourcesAndUnloadAppDomain` | `AppDomain.Unload` as the terminal shutdown step, and the `DomainUnload` notification it raises as the shutdown-completion signal | portable leaf: the unload is replaced by a direct `HostingEnvironment.CompleteShutdown()`, which runs the same completion sequence the `DomainUnload` handler ran, guarded to fire exactly once from either caller. **Deviation:** the application does not restart in place — the process must be recycled to host it again; see [process lifetime, shutdown, and recycle](follow-ups/process-lifetime-shutdown-and-recycle.md) | reached |

Consequences recorded elsewhere:

- H01 is satisfied by P04 and P05.
- H03 resolves to a null `_wpUserId`, because `WindowsIdentity.GetCurrent()`
  already fails into the existing `catch`. Whether that value has a supported
  meaning remains **research**.
- H04, H11, and H18 are inactive through P04 rather than through configuration:
  `IsEngineLoaded` is false, so perf counters, native ETW, and IIS version
  discovery early-out.
- H10 is only partially met by P08; generation-specific work storage is still
  outstanding and H10 must not be marked verified.
- H16 is reached by P15 with an explicit deviation.
- H12 is exercised: `trust level="Full"`, `legacyCasModel="false"`; partial trust
  and legacy CAS raise an explicit unsupported error.
- H17 executes; the fixture's precompiled handler means dynamic compilation is
  never triggered during initialization.
- R01 is reached — `HttpRuntime.ProcessRequest` receives request ownership.
  Module, handler, and response parity are not claimed.
- A07 is reached by P26. Before P26, `AppDomain.Unload` threw
  `CannotUnloadAppDomainException` on every iteration of the method's `for (;;)`
  loop, spinning a thread pool thread forever and leaving
  `_activeHostingEnvCount` at 1, so `ApplicationManager.ShutdownAll` waited out
  its full `3000 × 100ms` drain.

A POC cross-check was performed against `../Portable.System.Web`. It is hazard
evidence only, explicitly not a source ([core runtime port
plan](core-runtime-port-plan.md):52): it renames `FEATURE_PAL` →
`NET10_0_OR_GREATER` file-by-file, so its `UnsafeNativeMethods.cs` stubs are dead
behind an unrenamed `FEATURE_PAL`, and its `GetNumProcessCPUs` equivalent returns
0 rather than a real value.

## Ledger update rule

For each implementation change:

1. select the reached row;
2. link exact Reference Source callers and consumers;
3. state the required postcondition before choosing treatment;
4. add the oracle/core/adapter probe identifier;
5. record any normalization or accepted deviation;
6. mark **verified** only after all required OS jobs pass.

If evidence changes a decision, update the ADR and ledger together. Do not
preserve a row merely because it was written first.
