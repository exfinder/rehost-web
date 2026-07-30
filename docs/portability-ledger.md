# Classic-path portability ledger

Status: reached edges only. Add an edge when an executable slice reaches it;
unreached work stays in its follow-up.

Each row records the incompatible leaf and its observable treatment. Callers and
Framework sequencing remain intact unless the row says otherwise.

Treatments:

- **keep** — unchanged managed behavior;
- **inactive** — existing initialized state has no active capability;
- **portable leaf** — replace only the platform operation;
- **unsupported** — fail explicitly when selected.

P01–P24 were exercised by `PortableParity.Host run` on macOS `arm64` and Windows
`x64`, both `net10.0`, with identical events, status, and empty stderr. Framework
behavior remains under `NETFRAMEWORK`.

P27–P32 were reached by `PortableParity.Host verify`, which now matches the
Framework `cold-sync` golden trace exactly.

P33 was reached by `AdapterParity.Host verify` on macOS `arm64` and Windows
`x64`, which serves the same sessions over Kestrel. No differential probe reaches
it: the bench calls `HttpRuntime.ProcessRequest` on a thread it owns, so nothing
posts to the application's synchronization context.

## Reached edges

| ID | Edge | Treatment and contract |
| --- | --- | --- |
| P01 | `ApplicationManager` type initialization | Legacy CAS `StrongName` use is unreachable outside the Framework-only secondary-AppDomain block. |
| P02 | `Misc.ReportUnhandledException` | Portable `EventSource` leaf preserves the original activation failure. |
| P03 | `SimpleApplicationHost` physical path | Use `Path.DirectorySeparatorChar`. |
| P04 | `HttpRuntime.StaticInit` engine load | Split optional IIS integration from managed initialization; `IsEngineLoaded` is false on every supported OS. |
| P05 | `HttpRuntime.Init` platform gate | Remove the unconditional Win32 requirement. |
| P06 | `HttpConfigurationSystem.EnsureInit` host cast | Retain the write-only Framework field only under `NETFRAMEWORK`. |
| P07 | `ApplicationImpersonationContext` | A zero application identity token leaves impersonation inert; explicit impersonation policy remains deferred. |
| P08 | `SetUpCodegenDirectory` | Portable leaf for the two Framework leaves: the CLR install directory, which holds no codegen root and is not writable, and `SetDynamicBase`/`DynamicDirectory`, which are inert here. The root resolves as host `CompilationTempDirectory`, then configured `tempDirectory`, then `{Path.GetTempPath()}/rehost-webforms-tempfiles`; a configured value disagreeing with the host option fails at preflight, and an unwritable root fails naming the path and its source instead of relocating to `%TEMP%`. `_codegenDir` gains a generation segment of eight hex characters derived from the application directory, so distinct applications never share a directory and a restart finds its previous output. A clean run writes `<segment>/hash/hash.web`, `<segment>/preStartInitList.web`, and `<segment>/profileoptimization.prof`, and repeated runs add nothing. |
| P09 | configuration map-path selection | The explicit hosting map is authoritative. No portable server authority exists for out-of-application paths. |
| P10 | `IISMapPath.GetInstance` | Throw actionable `PlatformNotSupportedException`. |
| P11 | `SystemInfo.GetNumProcessCPUs` | Use affinity-aware `Environment.ProcessorCount`. |
| P12 | `SRef` cache-size sampling | Return zero; cache-size trimming is inactive while memory-pressure trimming remains. |
| P13 | `FileUtil.DirectoryExists` | Use `File.GetAttributes`, preserving file/directory distinction. |
| P14 | scratch-file naming | Use `Environment.CurrentManagedThreadId`. |
| P15 | `SetAutogenKeys` | Retain Framework random-key fallback. Keys are process-scoped; stable restart/scale-out behavior requires explicit `<machineKey>`. See [machine-key work](follow-ups/machine-key-and-viewstate-bootstrap.md). |
| P16 | `MultiTargetingUtil` validation | Report implemented surface `4.8.1`; higher targets retain the original failure. |
| P17 | `AspNetMemoryMonitor` totals | Use `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`. |
| P18 | configured process-memory limit | IIS worker processes cannot exist; retain the physical-memory heuristic. |
| P19 | current process ID | Use `Environment.ProcessId` at the native declaration seam. |
| P20 | loaded main-module filename | Use `Environment.ProcessPath`; other module lookups return null. |
| P21 | low-memory pressure | Derive load from `GCMemoryInfo`. Before the first GC, zero matches the Framework native-failure fallback and self-corrects on update. |
| P22 | `CompilationLock` | Use a session-local `System.Threading.Mutex`; existing drain/status behavior remains. |
| P23 | file enumeration metadata | Use `DirectoryInfo`/`FileSystemInfo`. Unix dot-files and Windows hidden attributes differ; content-selection policy remains [filesystem work](follow-ups/portable-filesystem-and-config-path-semantics.md). |
| P24 | C++ CodeDOM provider exclusion | Provider is absent outside .NET Framework; the lookup is inactive. |
| P25 | child configuration path combination | Use `Path.DirectorySeparatorChar`; `UserMapPathTests` covers the branch not reached by the harness. Other callers remain later-slice work. |
| P26 | `ReleaseResourcesAndUnloadAppDomain` | Call guarded `HostingEnvironment.CompleteShutdown()` directly. The application cannot restart in place; replacement requires a new process. See [process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md). |
| P27 | `InitializeHealthMonitoring` | The native deadlock watchdog recycled the worker process; no portable consumer exists and process replacement is host-owned. The `ProcessModelSection` read is retained so its validation still runs. |
| P28 | `HttpResponseUnmanagedBufferElement` | Replace the native buffer pool with an `ArrayPool<byte>`-backed managed element. The rented array never escapes: `Send` and `GetBytes` both surrender an exact-size copy, because `byte[]` recipients retain what they receive while `ClearBuffers` recycles on every flush. Framework avoided the copy through refcounted ownership transfer, which the `byte[]` overload cannot express. See [response buffer ownership](follow-ups/response-buffer-ownership.md). |
| P29 | `SetMinRequestsExecutingToDetectDeadlock` | Same native watchdog as P27; the executing-request threshold has no consumer. |
| P30 | `InitFusion` private-bin probing | `AppendPrivatePath`, `SetShadowCopyPath`, and `SetCachePath` are loader no-ops here. Resolve application `bin` through one immutable fallback on the default `AssemblyLoadContext`, so runtime-owned assemblies win first as the GAC did. Shadow copying is gone: `bin` assemblies stay file-locked while running. |
| P31 | `HttpRuntime.FinishRequest` error reporting | Framework discards both the request exception and the reporting failure with no record. The port reports them on `WebFormsRuntimeEventSource`. No observable response difference. |
| P32 | `BuildManager` preserved hash file path | `"hash\\hash.web"` produced one backslash-named file off Windows instead of nesting under the `hash` directory the cache creates. Use `Path.Combine` segments; same defect class as P25. |
| P33 | `SafeNativeMethods.GetCurrentThreadId` | `kernel32!GetCurrentThreadId` threw `DllNotFoundException` off Windows out of `HttpApplicationStateLock`, whose recursive write lock compares the value against one it recorded on the same thread. Portable leaf: `Environment.CurrentManagedThreadId`. Only identity is required, and the managed id is the stabler identity — the native id was never guaranteed constant for a managed thread. The remaining call sites build diagnostic strings. |

## Open path

- Activation, modules, handlers, response, and completion:
  [first runnable request](follow-ups/first-runnable-request.md).
- Remaining initialization edges:
  [request startup](follow-ups/request-startup-portability.md).
- Dynamic output and filesystem semantics:
  [runtime codegen](follow-ups/runtime-codegen-and-loading.md).
- Drain, disposal, unload, and recycle:
  [process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md).
- Remaining discarded exceptions in imported source:
  [silent exception swallowing](follow-ups/silent-exception-swallowing.md).
- Illogical `CallContext` isolation, and with it `HttpContext.Current` outside
  the request thread:
  [illogical call context isolation](follow-ups/illogical-call-context-isolation.md).

## Update rule

For each reached edge, record the required postcondition, incompatible leaf,
treatment, focused test, and accepted deviation. Mark behavior verified only
after every required OS job and differential probe passes.
