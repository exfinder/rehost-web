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

P40 is exercised by `ApplicationConfigurationPublicationTests`, which activates
one application declaring `targetFramework` and one declaring none, and reads
`ValidationSettings.UnobtrusiveValidationMode` back from each.

P43 is exercised by `BinaryFormatterEnablementTests`, which asserts the host
runtime configuration carries the switch and that `BinaryFormatter` resolves to
the out-of-band assembly rather than the shared framework's throwing stub.

P39 is exercised by `PageCompilationTests`, which compiles a page carrying a
literal run past the threshold and asserts the generated assembly references
none of `WriteUTF8ResourceString`, `CreateResourceBasedLiteralControl`, or
`SetStringResourcePointer`, and carries no Win32 resource directory.

P22 and P34–P38 are exercised by `CodegenSubstrateTests`, which activates real
applications in child processes on both platforms. No differential probe reaches
them: the slice-2 gate is port-local by
[ADR 0043](adr/0043-gate-the-compilation-substrate-locally.md).

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
| P08 | `SetUpCodegenDirectory` | See [ADR 0042](adr/0042-derive-the-codegen-directory-from-the-application.md). Portable leaf for the two Framework leaves: the CLR install directory, which holds no codegen root and is not writable, and `SetDynamicBase`/`DynamicDirectory`, which are inert here. The root resolves as host `CompilationTempDirectory`, then configured `tempDirectory`, then `{Path.GetTempPath()}/rehost-webforms-tempfiles`; a configured value disagreeing with the host option fails at preflight, and an unwritable root fails naming the path and its source instead of relocating to `%TEMP%`. `_codegenDir` gains a generation segment of eight hex characters derived from the application directory, so distinct applications never share a directory and a restart finds its previous output. A clean run writes `<segment>/hash/hash.web`, `<segment>/preStartInitList.web`, and `<segment>/profileoptimization.prof`, and repeated runs add nothing. |
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
| P22 | `CompilationLock` | Use a session-local `System.Threading.Mutex`; existing drain/status behavior remains. The mutex name is derived from a string hash, so cross-process exclusion also depends on P38. |
| P23 | file enumeration metadata | Use `DirectoryInfo`/`FileSystemInfo`. Unix dot-files and Windows hidden attributes differ; content-selection policy remains [filesystem work](follow-ups/portable-filesystem-and-config-path-semantics.md). |
| P24 | C++ CodeDOM provider exclusion | Provider is absent outside .NET Framework; the lookup is inactive. |
| P25 | child configuration path combination | Use `Path.DirectorySeparatorChar`; `UserMapPathTests` covers the branch not reached by the harness. Other callers remain later-slice work. |
| P26 | `ReleaseResourcesAndUnloadAppDomain` | Call guarded `HostingEnvironment.CompleteShutdown()` directly. The application cannot restart in place; replacement requires a new process. See [process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md). |
| P27 | `InitializeHealthMonitoring` | The native deadlock watchdog recycled the worker process; no portable consumer exists and process replacement is host-owned. The `ProcessModelSection` read is retained so its validation still runs. |
| P28 | `HttpResponseUnmanagedBufferElement` | Replace the native buffer pool with an `ArrayPool<byte>`-backed managed element. The rented array never escapes: `Send` and `GetBytes` both surrender an exact-size copy, because `byte[]` recipients retain what they receive while `ClearBuffers` recycles on every flush. Framework avoided the copy through refcounted ownership transfer, which the `byte[]` overload cannot express. See [response buffer ownership](follow-ups/response-buffer-ownership.md). |
| P29 | `SetMinRequestsExecutingToDetectDeadlock` | Same native watchdog as P27; the executing-request threshold has no consumer. |
| P30 | `InitFusion` private-bin probing | `AppendPrivatePath`, `SetShadowCopyPath`, and `SetCachePath` are loader no-ops here. Resolve application `bin` and the codegen directory through one immutable fallback on the default `AssemblyLoadContext`, so runtime-owned assemblies win first as the GAC did. Generated output is probed before `bin`, satellites in their culture subdirectory, and a `.delete`-marked file is never served. Shadow copying is gone: `bin` assemblies stay file-locked while running. |
| P31 | `HttpRuntime.FinishRequest` error reporting | Framework discards both the request exception and the reporting failure with no record. The port reports them on `WebFormsRuntimeEventSource`. No observable response difference. |
| P32 | `BuildManager` preserved hash file path | `"hash\\hash.web"` produced one backslash-named file off Windows instead of nesting under the `hash` directory the cache creates. Use `Path.Combine` segments; same defect class as P25. |
| P33 | `SafeNativeMethods.GetCurrentThreadId` | `kernel32!GetCurrentThreadId` threw `DllNotFoundException` off Windows out of `HttpApplicationStateLock`, whose recursive write lock compares the value against one it recorded on the same thread. Portable leaf: `Environment.CurrentManagedThreadId`. Only identity is required, and the managed id is the stabler identity — the native id was never guaranteed constant for a managed thread. The remaining call sites build diagnostic strings. |
| P34 | `CompilerResults.CompiledAssembly` | The .NET implementation loads through `Assembly.LoadFile`, which creates a load context per assembly, so `Global.asax` and `App_Code` would hold separate identities of the same type. Portable leaf: generated assemblies enter through `GeneratedAssemblyLoader` into the default context. Taken at `BuildProvider.CreateBuildResult`, which every build provider reaches, and at `CodeDirectoryCompiler`, which does not. The same edit in `BrowserCapabilitiesCodeGenerator` is inert while that file is excluded from the build. |
| P35 | `CodeDirectoryCompiler` stale-module wait | `kernel32!GetModuleHandle` threw `DllNotFoundException` off Windows before every `App_Code` load. It guarded against loading an assembly already loaded from that path, which `LoadFromAssemblyPath` would silently return in place of the new file. Portable leaf: ask the default context, and report Framework's `Assembly_already_loaded` immediately. The 3-second retry window is dropped because nothing unloads here, so waiting cannot change the outcome. |
| P36 | `MapPathActual` trailing separator | A virtual path with a trailing slash had a literal `"\\"` appended to its mapped physical path, and `UrlPath.PathEndsWithExtraSlash` recognized only that character. Off Windows every directory-existence check therefore ran against `.../name/\\` and reported absent, which made a configured `App_Code` subdirectory look missing. Use `Path.DirectorySeparatorChar` in both places; the Windows result is unchanged. Same defect class as P25 and P32. |
| P37 | `HashCodeCombiner` encoding identity | `Encoding.GetHashCode()` folds in its fallback's hash, which hashes a string and is randomized per process, so the top-level file hash differed on every run and preserved build results could never be reused. Add a typed overload combining `CodePage` and `WebName`, which every call site selects by overload resolution. |
| P38 | `StringUtil.GetNonRandomizedHashCode` | Both helpers shortcut to `string.GetHashCode()` and `StringComparer` when `UseRandomizedStringHashAlgorithm` is off, which .NET Framework left non-randomized but .NET randomizes per process. Every identity derived from them differed between processes: the `CompilationLock` mutex name, so no cross-process lock existed at all; `Page` hash codes; and auto-generated machine-key names. Always use the stable algorithm the method already carries. Framework keeps its branches. |
| P39 | `StringResourceManager` | **inactive.** Literal markup of 256 characters or more was emitted as a Win32 resource blob and read back by `ReadSafeStringResource` through `GetModuleHandle`, `FindResource`, and `LockResource`, which address the memory-mapped module image and exist only on Windows. `RoslynCSharpCodeProvider` reports `GeneratorSupport.Win32Resources` as absent, which is the capability `BaseTemplateCodeDomTreeGenerator.UseResourceLiteralString` already consults, so the generator emits an ordinary `Write` of a metadata string and produces no resource file. No imported source is edited. `StringResourceManager`, `SafeStringResource`, `ResourceBasedLiteralControl`, and `TemplateControl.ReadStringResource` become unreachable rather than portable. The accepted deviation is the lost optimization: `HttpWriter.WriteUTF8ResourceString` copied resource bytes straight into the response buffer, and a UTF-16 metadata string is transcoded per request instead. Rendered bytes are unchanged. A portable `PEReader` reader is proven and costed in [runtime codegen](follow-ups/runtime-codegen-and-loading.md) if the saving is ever wanted. |
| P40 | `CreateAppDomainWithHostingEnvironment` application configuration | Framework read the application's configuration and published it into the child AppDomain's data before creating it, because sections such as `<compilation>` load before config is baked and cannot depend on `<httpRuntime>`. The portable path returns a hosting environment in the current AppDomain and had replaced that whole body, so none of it was published: `ASPNET_TARGETFRAMEWORK`, `REGEX_DEFAULT_MATCH_TIMEOUT`, `.defaultObjectCacheProvider`, `.devEnvironment`, and the `fcnMode` and `aspnet:DisableFcnDaclRead` hosting parameters. `BinaryCompatibility.Current` therefore reported pre-4.5 for every application whatever it declared, which left `machineKey compatibilityMode` at `Framework20SP1` and would have sent view state down the legacy native crypto path. Portable leaf: `PublishApplicationConfiguration` publishes the same values to the current AppDomain before the hosting environment is created, opening the application configuration through the host's own file map rather than Framework's, which points at the CLR's machine.config location. Trust, CAS, impersonation, IIS, and the AppDomain switches are omitted as unreachable. Framework's `Require_stable_string_hash_codes` check is omitted too: it compares `StringComparer.InvariantCultureIgnoreCase.GetHashCode` against a Framework constant, so it is permanently true here and would reject every application. An application declaring no `targetFramework`, or one below 4.5, is refused here naming the fix, rather than running with quirks this port has not assessed. The comparison is made by hand: touching `BinaryCompatibility` would run its static constructor and capture `Current` before the value is published, freezing it at pre-4.5. Failures here keep Framework's contract rather than aborting: it caught everything this block could throw and passed it to `HostingEnvironment.Initialize`, which stashes it and renders it on every request, so the portable path threads the same exception through. `ConfigurationBuilders.IgnoreLoadFailure` is published too; it was the one value in that block Framework already set on the current AppDomain rather than the child. |
| P41 | `UnsafeNativeMethods` | **unsupported.** 208 `DllImport` entry points into `webengine4.dll`, `aspnet_isapi.dll`, and Win32, none of which has a portable replacement: the class carries no `#if NETFRAMEWORK` branches, unlike `SafeNativeMethods`, whose `GetCurrentProcessId` and `GetCurrentThreadId` are replaced and reached. A throwing static constructor refuses the type as a whole, so an unported path reports one diagnostic instead of a `DllNotFoundException` naming a library that will never exist. Constants inline at compile time and are unaffected, and `INVALID_HANDLE_VALUE` is read only from `#if NETFRAMEWORK` code. Nothing reached calls it: the full suite passes on both platforms with the constructor in place. |
| P43 | `EnableViewStateMacRegistryHelper` | **inactive**, fail-safe. `IsMacEnforcementEnabledViaRegistry` reads `HKLM\SOFTWARE\Microsoft\.NETFramework\v{Environment.Version.ToString(3)}`, which resolves to `v10.0.0` here — a key that exists on no machine — and `Microsoft.Win32.Registry` is unusable off Windows in any case. Both platforms therefore take the `catch`, which returns `true`, so `EnforceViewStateMac` is on and a page setting `EnableViewStateMac = false` is ignored. That matches a patched Framework machine, where the key is set, so no deviation is observable; it is recorded because the result comes from the version string rather than from a decision, and a future change to the key name would silently disable enforcement. `aspnet:AllowInsecureDeserialization` still overrides, as on Framework. |

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
