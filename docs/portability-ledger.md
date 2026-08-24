# Classic-path portability ledger

Add an edge when executable application work reaches it; unreached work stays
in the [backlog](backlog.md).

Each row records the incompatible leaf and its observable treatment. Callers and
Framework sequencing remain intact unless the row says otherwise.

Treatments:

- **keep** — unchanged managed behavior;
- **inactive** — existing initialized state has no active capability;
- **portable leaf** — replace only the platform operation;
- **unsupported** — fail explicitly when selected.

The focused tests named below and the full solution run on Windows x64, Linux
x64, and macOS arm64. The frozen Framework and adapter parity gates cover the
original managed pipeline boundary; see [parity rigs](../tests/parity/README.md).

P40 is exercised by `ApplicationConfigurationPublicationTests`, which activates
one application declaring `targetFramework` and one declaring none, and reads
`ValidationSettings.UnobtrusiveValidationMode` back from each.

P49 is exercised by `BinaryFormatterEnablementTests`, which asserts that a
consuming application's own runtime configuration carries the switch and that
`BinaryFormatter` resolves to the out-of-band assembly rather than the shared
framework's throwing stub.

P44 is exercised by `RecycleLimitMonitorTests`, which invokes the real monitor
callback and verifies its sample against the current process.

P48 is exercised by `PostbackOverKestrelTests`, which pins the rendered
`__VIEWSTATEGENERATOR` to `72DAA2F9` for the fixture page, and by
`ClientStateIdentifierTests`, which recomputes that value from the two inputs
`GetClientStateIdentifier` combines. The rendered constant alone would only show
the value is stable, since it was taken from this port's own output; recomputing
it shows the stable algorithm produced it. Framework's value for the same page
was since measured on 4.8.1 as `CA0B0334`, confirming the divergence rather than
merely predicting it, and a payload rendered on either runtime was accepted by
the other under a shared literal key — so the divergence does not reach the view
state key. The error-path divergence and mixed-farm boundary are in the
[compatibility map](compatibility.md#state-security-and-ancillary-assemblies).

P51 is exercised by `TimeoutSweepOverKestrelTests`, whose probe page runs the
timeout sweep with its own request presented as expired and renders a marker
only if the sweep left it running.

P53 is exercised by `TimeoutSweepOverKestrelTests`, whose probe page presents
its own request as expired to the real sweep and asserts boundary delivery, the
one-shot flag, and the `ThreadAbortOnTimeout` opt-out, and by
`TimeoutOverKestrelTests`, where the real timer chain fires against
`executionTimeout="1"` with a 2-second scan.

P55 is exercised by `HeaderAmendmentOverKestrelTests`, whose EndRequest stamp
must reach the wire after `End` and a terminating `Redirect`, while an
application `Flush()` still seals and `CompleteRequest` stays open.

P61 and P62 are exercised by `ServerTransferOverKestrelTests`: the static file
Execute serves through the split, the refusal message reaches the page's
catch, and the Transfer/Execute shapes around them pin the unchanged imported
behavior.

P63 is exercised by `AsyncPagesOverKestrelTests`, whose per-stage sc/ctx
expectations are a win-oracle 4.8.1 reading of the same fixture pages
(restored on sync-context resumes, null after `ConfigureAwait(false)`,
restored at render); the always-null restorer mutation turns both restore
tests red. P64 is exercised by the `ApplicationBootstrapTests` preflight
pair (explicit false refused with the setting named, explicit true accepted).

P60 is exercised by `IisServerConfigurationTests` (the measured collection
semantics: duplicate-add error naming the file, tolerated remove, clear) and
`WebServerAmendmentsOverKestrelTests` (an application's amendments end to
end: added and removed extensions, extended and un-hidden segments, an
unhonored section tolerated); the IIS-map content types are pinned by
`StaticFilesOverKestrelTests` (`.js` as `application/javascript`, `.svg`
rendering at all).

P59 is exercised by `HiddenSegmentsOverKestrelTests`: every segment refuses a
file that genuinely exists, at depth and in foreign casing, while a name
merely containing a segment still serves; the refusals fail with the
`ValidatePath` hook removed.

P58 is exercised by `StaticFilesOverKestrelTests` (the conditional matrix,
the off-list 404, and the unmapped-known-extension serve — the 304 cases and
the gate case fail with their additions removed) and by
`AspNetCoreWorkerRequestTests`, where a file range spools in order beside
memory writes and is read at commit, not at spool time.

P56 is exercised by `FileUtilTests`, whose cases fail on the pre-completion
code on the platform each fix targets: separator stripping and appending,
the stable truncation suffix, and the managed relative-name walk.

P57 is exercised on a real case-sensitive filesystem (a disposable
case-sensitive APFS volume on macOS; Linux temp natively; skipped on NTFS):
`CanonicalCasePathTests` at the resolver, and
`CaseInsensitiveUrlOverKestrelTests` end to end — a wrongly-cased URL for a
page no other request has compiled, the collision 500 naming both files, and
the unchanged 404. Both hook-sensitive scenarios fail with the `MapPathActual`
hook removed.

P52 is exercised by `ResponseEndOverKestrelTests`, whose probe pages call
`Response.End` plainly, under a swallowing `catch (Exception)`, under
`catch (ThreadAbortException)`, via `Response.Redirect`, and from a module
event, asserting the response bytes and the witness stage list.

P42 is exercised by `ViewStateSerializationTests`. Its Framework reading was
taken by reflecting over the GAC `System.Web` on the Windows host:
`StateBag.SaveViewState` returns `IndexedString` keys and `LosFormatter`
implements no interfaces.

P39 is exercised by `PageCompilationTests`, which compiles a page carrying a
literal run past the threshold and asserts the generated assembly references
none of `WriteUTF8ResourceString`, `CreateResourceBasedLiteralControl`, or
`SetStringResourcePointer`, and carries no Win32 resource directory.

P73 is exercised by `FileEnumeratorTests` (eleven names created out of order,
digits and `_` pinning the code-unit rule, a case-only pair on the
case-sensitive volume), `BinDirectoryScanTests` (native, empty, text, `.DLL`,
`.exe`, and managed files in one `bin`), and
`CodegenCompileErrorTests.Blames_Duplicate_App_Code_Types_In_Directory_Order`
end to end; all three fail on APFS and ext4 with the sort removed.

P80 is exercised by `WebSocketsOverKestrelTests` (a `ClientWebSocket` handshake
carrying the cookie and headers, the callback's report, echo, both close
directions, sub-protocol on the handshake and the socket, discarded post-accept
body, the 500/403/400 refusals by raw handshake, and the BeginRequest refusal
through `Global.asax`), the accept cases in `AspNetCoreWorkerRequestTests`, and
`HttpContextWebSocketsTests` for the unchanged refusal on a worker request
without the capability.

P79 is exercised by `StreamingOverKestrelTests` (a raw-socket reader
timestamps each arrival: the head and first chunk before the handler's delay,
the second chunk after it; End, TransmitFile, late-header, error-after-flush,
and async-flush shapes against the readings) and the flush cases in
`AspNetCoreWorkerRequestTests`; the two timing scenarios fail with
`FlushResponse` emptied.

P76 and P77 are exercised by `AspNetCoreWorkerRequestTests`,
`RequestHeaderEncodingTests`, `ServerVariablesOverKestrelTests` (Host header,
loopback-forwarded headers, the three request-header byte shapes, the default
response-header bytes) and `ResponseHeaderEncodingOverKestrelTests` (an
`iso-8859-1` application copy); P78 by `ResponseSpoolTests`,
`Http2OverKestrelTests`, and `HttpContextWebSocketsTests`.

P74 is exercised by `FileUtilTests` (a canonical 300-character path is not
suspicious, the same path with `..` inside is) and `SimpleWorkerRequestTests`;
P75 by `WebManagementEventTests`. All three fail on macOS with the fixes
removed.

P22 and P34–P38 are exercised by `CodegenSubstrateTests`, which activates real
applications in child processes. These are port-local gates under the
[evidence strategy](adr/0005-evidence-and-test-strategy.md).

P33 is exercised by the adapter parity gate over Kestrel. The bench calls
`HttpRuntime.ProcessRequest` on a thread it owns, so nothing posts to the
application's synchronization context.

P66 is exercised by
`DesignTimeMetadataOverKestrelTests.Controls_Carrying_Toolbox_Metadata_Parse_And_Render`,
whose fixture page declares all six controls carrying the attribute. Giving the
internal shape a throwing type initializer — what the real attribute does off
Windows — turns it red, and nothing else in the suite notices.

P70 is exercised on the case-sensitive volume by
`CaseSensitiveDirectoryConfigOverKestrelTests.A_Directory_Web_Config_Denies_The_Request`,
whose `page` fixture carries `Guarded/Web.config` beside `Guarded/Secret.aspx`.
Dropping either fold serves the page instead of refusing it, and no other test
notices; on Windows/NTFS the situation cannot be expressed and it skips.

P71 is exercised by `ServerIncludesOverKestrelTests` on the shared `page` host
(an include climbing above the root in both separator spellings, red on
macOS/Linux with the separator fix removed, green on Windows before and after),
`PathCasingOverKestrelTests` on the case-sensitive volume (a wrongly-cased
escaping include, red with the out-of-root fold removed; a wrongly-cased
`<pages masterPageFile>`, `Web.sitemap` under the provider's `web.sitemap`
default, and wrongly-cased rooted `DataFile`/`BodyFileName`/`WriteFile` paths
through the existing seams), `CanonicalCasePathTests` for the out-of-root
fold at the resolver, and `PathClassificationOverKestrelTests`, which pins the
IIS Express reading table for the audited sites on all three OSes.

P72 is exercised by `RequestPathCanonicalizerTests` and
`AspNetCoreWorkerRequestTests` (Hosting) and `RequestPathInfoTests` (Runtime) row by
row against the reading, and wired end to end by
`PathCanonicalizationOverKestrelTests` on the shared `page` host through the parity
`PathProbeHandler` — path-info split with the hidden-segment rule confined to the
file path, canonical `RawUrl`, `%2F` serving a static file, and the front-door 403.

P69 is exercised by
`ModelBoundListViewOverKestrelTests.ListView_Binds_Its_ItemType_Select_Method`,
whose fixture page declares the `ItemType` that reaches the load. Restoring the
unguarded `Assembly.Load` turns it red at `OnInit`, before any binding runs.

P50 is exercised by `SaveAsPathTests` and
`UploadSaveOverKestrelTests.Refuses_A_Windows_Path_Where_It_Cannot_Be_Rooted`.
The refusal executes on macOS/Linux; Windows proves the portable guard is inert.

P68 is exercised by `ResponseHeaderCollectionTests` at the seam (the generated
block for every precedence case, and the post-send collection) and by
`ResponseHeadersOverKestrelTests`, one test per reading H1-H16 over the shared
page host's `ResponseHeadersProbe`, which reproduces the IIS probe's stimuli and
dumps the same lines. Dropping the collection from generation turns eight of
them red; letting a collection entry outrank the field- and policy-generated
headers turns five.

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
| P08 | `SetUpCodegenDirectory` | See [codegen storage](adr/0008-codegen-storage.md). Portable leaf replaces the unavailable CLR install/dynamic-directory roots. Host option, application configuration, and portable default resolve deterministically; conflicts and unwritable roots fail explicitly. A stable application-derived segment preserves Framework reuse and separates applications. |
| P09 | configuration map-path selection | The explicit hosting map is authoritative. No portable server authority exists for out-of-application paths. |
| P10 | `IISMapPath.GetInstance` | Throw actionable `PlatformNotSupportedException`. |
| P11 | `SystemInfo.GetNumProcessCPUs` | Use affinity-aware `Environment.ProcessorCount`. |
| P12 | `SRef` cache-size sampling | **unsupported.** `System.SizedReference` measured a whole object graph after each gen2 collection, which is how `<cache privateBytesLimit>` bounded the cache itself rather than the process. No .NET type replaces it and the GC handle kind it was built on was withdrawn, so `GCHandle.Alloc(o, (GCHandleType)5)` throws. Microsoft's own `System.Runtime.Caching` ships the same gap silently: its `SRef` is absent from the .NET build, `MemoryCache.Default.CacheMemoryLimit` reports the 1 TB placeholder, and 300 MB of entries survive a forced collection untrimmed. `ApproximateSize` still returns zero so the sampling stays inert, but a non-zero `privateBytesLimit` is now refused at `CacheSizeMonitor.ReadConfig` naming `<processModel memoryLimit>`, rather than accepted and ignored. The default is absent from the Web Forms project template, so only an application that opted in deliberately is affected. Process-level trimming is unaffected and covered by P18 and P44. |
| P13 | `FileUtil.DirectoryExists` | Use `File.GetAttributes`, preserving file/directory distinction. |
| P14 | scratch-file naming | Use `Environment.CurrentManagedThreadId`. |
| P15 | `SetAutogenKeys` | Retain Framework random-key fallback. Keys are process-scoped; stable restart/scale-out behavior requires explicit `<machineKey>`. Framework reports nothing when a key resolves to `AutoGenerate`, because its own came from a machine-wide store and did survive a restart; here the same configuration silently invalidates every protected payload when the process ends, so preflight raises one `WebFormsRuntimeEventSource` event naming the consequence. The event is the whole addition: no response, status, or timing differs. See [machine-key work](follow-ups/machine-key-and-viewstate-bootstrap.md). |
| P16 | `MultiTargetingUtil` validation | Report implemented surface `4.8.1`; higher targets retain the original failure. |
| P17 | `AspNetMemoryMonitor` totals | Derive the total from `HighMemoryLoadThresholdBytes * 100 / GC.GetConfigurationVariables()["GCHighMemPercent"]`. `TotalAvailableMemoryBytes` was wrong: it is the collector's heap ceiling, which a container limit reduces to 75% of itself, while `MemoryLoadBytes` stays measured against the undiminished limit. The threshold shares `MemoryLoadBytes`' basis and its percentage is readable and honours `System.GC.HighMemoryPercent`, so the two recover the basis exactly — verified as 16 GiB on macOS `arm64`, 32 GiB on Windows `x64`, and the declared cap in Linux containers at 512 MiB and 1 GiB. Unlike `MemoryLoadBytes` the threshold is populated before the first collection, so the total is known at startup. |
| P18 | configured process-memory limit | Both Framework sources were the hosting worker process, and no other existed, so the property returned zero and the auto heuristic was the only limit — untunable. Read `<processModel memoryLimit>`, whose declared meaning is the share of memory the process may hold before recycling, and apply `min(percent * total, total - 256MB)` with the subtrahend floored at half the total. Framework's heuristic remains under `NETFRAMEWORK`. No percentage reproduces Framework's observable behaviour: 60% of a shared server's whole RAM sat so far above any real working set that the monitor effectively never ran, whereas 60% of a container's own allocation can be crossed at startup by an application that compiles pages. The shipped default is therefore 80, set in `rehost-webforms.machine.config` rather than by editing the section's declared default of 60. The absolute reserve exists because the room a trim and a compacting collection need does not scale with the container, so a percentage alone leaves too little of it on a small one. |
| P19 | current process ID | Use `Environment.ProcessId` at the native declaration seam. |
| P20 | loaded main-module filename | Use `Environment.ProcessPath`; other module lookups return null. |
| P21 | low-memory pressure | Divide `MemoryLoadBytes` by the P17 total rather than by `TotalAvailableMemoryBytes`, which overstated load by a third inside a container and reported a true 75% as 100%. Before the first GC, zero matches the Framework native-failure fallback and self-corrects on update. The 95–99% threshold table is replaced by the collector's own high-memory percentage, 90 by default, and `<cache percentagePhysicalMemoryUsedLimit>` still overrides. That table was derived from Windows' low-memory notification, which fires while a pagefile still absorbs the overshoot; Kubernetes, ECS and Fargate run without swap, so 99% of a 4 GiB limit left 41 MB in which to trim and compact before the kernel kills the process. |
| P22 | `CompilationLock` | Use a session-local `System.Threading.Mutex`; existing drain/status behavior remains. The mutex name is derived from a string hash, so cross-process exclusion also depends on P38. |
| P23 | file enumeration metadata | Use `DirectoryInfo`/`FileSystemInfo`. Unix dot-files and Windows hidden attributes differ; content-selection policy remains [filesystem work](filesystem-semantics.md). |
| P24 | C++ CodeDOM provider exclusion | Provider is absent outside .NET Framework; the lookup is inactive. |
| P25 | child configuration path combination | Use `Path.DirectorySeparatorChar`; `UserMapPathTests` covers the branch not reached by the harness. Other callers remain later-slice work. |
| P26 | `ReleaseResourcesAndUnloadAppDomain` | Call guarded `HostingEnvironment.CompleteShutdown()` directly. The application cannot restart in place; replacement requires a new process. See [process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md). |
| P27 | `InitializeHealthMonitoring` | The native deadlock watchdog recycled the worker process; no portable consumer exists and process replacement is host-owned. The `ProcessModelSection` read is retained so its validation still runs. |
| P28 | `HttpResponseUnmanagedBufferElement` | Replace the native buffer pool with an `ArrayPool<byte>`-backed managed element. The rented array never escapes: `Send` and `GetBytes` both surrender an exact-size copy, because `byte[]` recipients retain what they receive while `ClearBuffers` recycles on every flush. Framework avoided the copy through refcounted ownership transfer, which the `byte[]` overload cannot express. See [response buffer ownership](follow-ups/response-buffer-ownership.md). |
| P29 | `SetMinRequestsExecutingToDetectDeadlock` | Same native watchdog as P27; the executing-request threshold has no consumer. |
| P30 | `InitFusion` private-bin probing | `AppendPrivatePath`, `SetShadowCopyPath`, and `SetCachePath` are loader no-ops here. Resolve application `bin` and the codegen directory through one immutable fallback on the default `AssemblyLoadContext`, so runtime-owned assemblies win first as the GAC did. Generated output is probed before `bin`, satellites in their culture subdirectory, and a `.delete`-marked file is never served. Generated assembly reclamation guarantees logical deletion: immediate unlink is best-effort and a marker makes a surviving file unusable until a later sweep. Shadow copying is gone: `bin` assemblies stay file-locked while running. |
| P31 | `HttpRuntime.FinishRequest` error reporting | Framework discards both the request exception and the reporting failure with no record. The port reports them on `WebFormsRuntimeEventSource`. No observable response difference. |
| P32 | `BuildManager` preserved hash file path | `"hash\\hash.web"` produced one backslash-named file off Windows instead of nesting under the `hash` directory the cache creates. Use `Path.Combine` segments; same defect class as P25. |
| P33 | `SafeNativeMethods.GetCurrentThreadId` | `kernel32!GetCurrentThreadId` threw `DllNotFoundException` off Windows out of `HttpApplicationStateLock`, whose recursive write lock compares the value against one it recorded on the same thread. Portable leaf: `Environment.CurrentManagedThreadId`. Only identity is required, and the managed id is the stabler identity — the native id was never guaranteed constant for a managed thread. The remaining call sites build diagnostic strings. |
| P34 | `CompilerResults.CompiledAssembly` | The .NET implementation loads through `Assembly.LoadFile`, which creates a load context per assembly, so `Global.asax` and `App_Code` would hold separate identities of the same type. Portable leaf: generated assemblies enter through `GeneratedAssemblyLoader` into the default context. Taken at `BuildProvider.CreateBuildResult`, which every build provider reaches, and at `CodeDirectoryCompiler`, which does not. The same edit in `BrowserCapabilitiesCodeGenerator` is inert while that file is excluded from the build. |
| P35 | `CodeDirectoryCompiler` stale-module wait | `kernel32!GetModuleHandle` threw `DllNotFoundException` off Windows before every `App_Code` load. It guarded against loading an assembly already loaded from that path, which `LoadFromAssemblyPath` would silently return in place of the new file. Portable leaf: ask the default context, and report Framework's `Assembly_already_loaded` immediately. The 3-second retry window is dropped because nothing unloads here, so waiting cannot change the outcome. |
| P36 | `MapPathActual` trailing separator | A virtual path with a trailing slash had a literal `"\\"` appended to its mapped physical path, and `UrlPath.PathEndsWithExtraSlash` recognized only that character. Off Windows every directory-existence check therefore ran against `.../name/\\` and reported absent, which made a configured `App_Code` subdirectory look missing. Use `Path.DirectorySeparatorChar` in both places; the Windows result is unchanged. Same defect class as P25 and P32. |
| P37 | `HashCodeCombiner` encoding identity | `Encoding.GetHashCode()` folds in its fallback's hash, which hashes a string and is randomized per process, so the top-level file hash differed on every run and preserved build results could never be reused. Add a typed overload combining `CodePage` and `WebName`, which every call site selects by overload resolution. |
| P38 | `StringUtil.GetNonRandomizedHashCode` | Both helpers shortcut to `string.GetHashCode()` and `StringComparer` when `UseRandomizedStringHashAlgorithm` is off, which .NET Framework left non-randomized but .NET randomizes per process. Every identity derived from them differed between processes: the `CompilationLock` mutex name, so no cross-process lock existed at all; `Page` hash codes; and auto-generated machine-key names. Always use the stable algorithm the method already carries. Framework keeps its branches. |
| P39 | `StringResourceManager` | **inactive.** The configured Roslyn provider reports Win32 resources unsupported, so the existing generator emits long literal markup as metadata strings. Native module-resource lookup becomes unreachable. Rendered bytes stay unchanged; the accepted cost is transcoding instead of Framework's byte-copy optimization. |
| P40 | application configuration publication | **portable leaf.** Before activation, the host file map publishes target framework, regex/cache/environment switches, FCN settings, and configuration-builder behavior into the current AppDomain. Applications below target framework 4.5 fail actionably; CAS/IIS/AppDomain-only switches remain omitted. This keeps modern machine-key crypto selected without prematurely initializing `BinaryCompatibility`. |
| P41 | `UnsafeNativeMethods` | **unsupported.** 208 `DllImport` entry points into `webengine4.dll`, `aspnet_isapi.dll`, and Win32, none of which has a portable replacement: the class carries no `#if NETFRAMEWORK` branches, unlike `SafeNativeMethods`, whose `GetCurrentProcessId` and `GetCurrentThreadId` are replaced and reached. A throwing static constructor refuses the type as a whole, so an unported path reports one diagnostic instead of a `DllNotFoundException` naming a library that will never exist. Constants inline at compile time and are unaffected. `INVALID_HANDLE_VALUE` is a `static readonly` field, so the unfenced sites comparing against it (`FileAuthorizationModule`, `FileUtil`, `FileEnumerator`, `OutOfProcStateClientManager`, `ISAPIWorkerRequest`) trip the constructor and get the same refusal, which is the intended diagnostic. Reached supported behavior must move to an owned portable leaf, as P44 does. |
| P42 | `OBJECTSTATEFORMATTER` build flag | **keep.** Framework builds System.Web with this symbol defined; the port did not, so four files compiled their pre-2.0 branches. `StateBag` wrote raw string keys where Framework writes `IndexedString`, the token `ObjectStateFormatter` deduplicates into a string table, so view state payloads differed in bytes and size from Framework's. `LosFormatter` compiled as a separate pre-2.0 serializer implementing `IStateFormatter` instead of the thin wrapper over `ObjectStateFormatter`, `LosWriter` compiled at all, and `Page` collected control state in a `Hashtable` rather than a `HybridDictionary`. No imported source is edited; the symbol is added to `DefineConstants`. The Framework reading was taken by reflection over the GAC assembly rather than inferred. |
| P43 | ViewState-MAC registry policy | **inactive**, fail-safe. The Framework registry key cannot resolve on .NET 10/off Windows, so MAC enforcement and generator-field emission stay on; explicit `aspnet:AllowInsecureDeserialization` still overrides. This matches patched Framework hosts but can differ from an unpatched machine that omits `__VIEWSTATEGENERATOR`. |
| P44 | recycle-memory sampling | **portable leaf.** Native private-byte sampling crashed every platform from a timer; `Environment.WorkingSet` now supplies the cgroup-relevant resident measure. Windows private-byte semantics differ, and long-running container behavior remains on [memory validation](follow-ups/container-memory-validation.md). |
| P45 | induced collection | `GC.Collect()` frees the trimmed entries but leaves their pages committed, and a cgroup counts committed pages: measured in a 1 GiB container, trimming 200 MB and collecting returned 31 MB, so the cache was emptied and the pause paid while the kernel's view barely moved. Framework's own code anticipates this, deferring the next induced collection by 60 seconds when the last recovered under 1% of private bytes, and `GCCollectionMode.Aggressive` — which decommits — did not exist before .NET 5. Portable leaf: `GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true)` in both monitors, recovering all 200 MB. It stays blocking so Framework's pacing, which times each collection and targets 3.3% of wall clock, still measures it. Recovery measured at 99.3–100% on macOS `arm64` and 100% on Windows `x64` across twelve iterations. |
| P46 | monitor callback failure | Both monitor callbacks were `try`/`finally` with no `catch`, so any exception reached the timer thread and ended the process. Framework guarded only the observer notifications, whose comment names the consequence, because the sampling beneath was a native call that could not throw. Here the process holds the one application, and ending it for a failure in a subsystem that only observes discards every session and in-flight request. Report each failure on `WebFormsRuntimeEventSource`; after three consecutive failures stop the timer, report it, and write one line to standard error, because nothing subscribes to the event source by default and a monitor that has given up has withdrawn the protection silently. |
| P47 | `Counter` | `SafeNativeMethods.QueryPerformanceCounter` and `QueryPerformanceFrequency` are unguarded `kernel32` imports in a class that, unlike `UnsafeNativeMethods`, carries no refusing constructor, so `TraceContext` threw `DllNotFoundException` off Windows whenever page tracing was enabled. Portable leaf: `Stopwatch.GetTimestamp` and `Stopwatch.Frequency`, which are the same counter on Windows. The only caller divides one by the other, so the differing unit off Windows is not observable. |
| P48 | `__VIEWSTATEGENERATOR` identity | **keep**, accepted deviation. Framework uses an unavailable NLS hash; this runtime uses P38's stable arithmetic hash, so field values differ permanently. Validation is self-consistent and Framework-4.5 payload key derivation does not use this field; differentials assert shape, not value. |
| P49 | `BinaryFormatter` availability | **portable leaf.** .NET ships `BinaryFormatter` as a throwing stub, and `ObjectStateFormatter` falls through to it for any value with no string `TypeConverter`, so view state carrying such a value fails without it. The out-of-band `System.Runtime.Serialization.Formatters` package supplies a working implementation, and `EnableUnsafeBinaryFormatterSerialization` must be true before it is first read. A module initializer alone is not enough: the switch latches on first read, so an application that serialized before it first touched System.Web would keep the SDK default of `false` whatever the initializer did afterwards. The shipped targets therefore write the switch into the *consuming* application's runtimeconfig rather than this library's, which nothing runs. `ExcludeAssets="compile"` keeps the port's typerefs on the shared framework's `8.1.0.0` while the package supplies `10.0.0.0` at run time. Framework had no switch and no stub. Legacy serialization payloads are a trusted-input feature here, not a security boundary. |
| P50 | `SaveAs` rooted-path guard | **portable leaf**, additive. Both `SaveAs` overloads ask `Path.IsPathRooted`, whose answer is the running platform's, and Framework never ran off Windows. There a Windows path is rooted; elsewhere it names no file at all, so `requireRootedSaveAsPath` on refuses it as merely "not rooted", and off, the whole path becomes the name of one file in the working directory — a save that reports success and leaves nothing where the application looks. One port-owned check runs before Microsoft's block, unchanged beneath it, and refuses a path rooted on Windows but not here, naming the real problem and the fix. Configuration cannot express this: the setting toggles the generic check, and neither state produces a platform-aware answer. Inert on Windows by construction, so no Framework behavior changes. |
| P51 | `RequestTimeoutManager` request abort | **inactive.** `TimeoutIfNeeded` ended an expired request with `Thread.Abort`, which throws `PlatformNotSupportedException` on this runtime — unhandled on the manager's 15-second `Timer` thread, ending the process; `Stop`'s shutdown sweep reached the same call for every in-flight request. The sweep now decides nothing: no abort, no `_timeoutState` flip (its `-1` would leave `WaitForExceptionIfCancelled` spinning for an abort that never arrives), and no `TimedOutToken` cancellation, so `executionTimeout` was uniformly unenforced and a slow request ran to completion. The manager, its timer, and per-request registration stayed intact, which is what P53 later re-enabled cooperatively; the remaining `Thread.Abort` sites and the `SYSLIB0006` `NoWarn` removal were P52. |
| P52 | `Response.End` termination | **portable leaf.** A typed internal exception replaces unavailable thread abort after flush/end/`CompleteRequest`; pipeline boundaries recognize it and preserve response termination. Documented deltas: `catch (ThreadAbortException)` never runs, and non-response code inside a swallowing broad catch can run until the next boundary. Child-request termination remains unassessed. |
| P55 | header seal at `Response.End` | **portable leaf.** End defers header generation until final commit so `EndRequest` can add headers/cookies as IIS allowed, while an application `Flush` still seals immediately. Explicit flush after End seals early; a HEAD page calling End reports zero rather than the suppressed body length. |
| P53 | `executionTimeout` | **portable leaf.** The timeout scan sets a per-request flag; the next pipeline-step boundary throws P52's timeout exception and cancels `TimedOutToken`. Delivery is cooperative: a running synchronous step must return and a never-returning step cannot be stopped; connection abort is separate follow-up work. Scan period is `rehost:RequestTimeoutScanSeconds`, default 15 seconds. |
| P56 | `FileUtil` portable completion | **portable leaf.** Per [filesystem semantics](filesystem-semantics.md), separator handling is platform-aware, codegen path truncation uses the stable P38 hash, and the remaining compiled `FindFirstFile` path becomes a managed parent walk. Framework bodies remain under `NETFRAMEWORK` where whole methods changed. |
| P57 | URL-to-file case folding | **portable leaf**, additive. [Filesystem semantics](filesystem-semantics.md) defines exact-match-first canonicalization, case-insensitive fallback, deterministic collision failure, unchanged genuine misses, and pass-through outside the application root. Two seams produce a physical path: `HostingEnvironment.MapPathActual`, and `HttpRequest.PhysicalPathInternal` where a worker request's own concatenation enters. Only the first was folded, so static files — which `StaticFileHandler` resolves through `Request.PhysicalPath` — missed on a case-sensitive filesystem while pages served, and every wrongly-cased asset URL a Windows-hosted application had accumulated answered 404 on the deployment target. |
| P62 | `Server.TransferRequest` refusal | **explicit contract.** Integrated-only API under the integrated-mode oracle (IIS-integration decision 0): under IIS integrated pipeline it performed a full child-request re-entry, which this host has no pipeline to honor, and classic Framework refused it with "requires IIS integrated pipeline mode" — advice no configuration of this host can follow. Resolved as explicit refusal with the change the caller can make: the guard keeps `PlatformNotSupportedException` but names `Server.Transfer`/`Server.Execute` as the replacements. Implementing the re-entry toward integrated behavior stays an owned follow-up to take if a real application needs it. `Server.Transfer` and `Server.Execute` themselves behave identically in both modes and carried no mode decision; every shape probed worked unchanged (child render, parent-tail suppression through the P52 termination unwind, query preserve/override/clear, `PreviousPage`, writer capture, the `IHttpAsyncHandler` arm every Execute takes). |
| P61 | `Server.Execute` to a static file | **portable leaf.** `ExecuteInternal` holds a translated physical path, so it enters `WriteFileTranslated` past Framework's Windows-only physical/virtual classifier. The public `WriteFile` surface stays unchanged; see [filesystem semantics](filesystem-semantics.md). |
| P60 | IIS configuration foundation | **portable leaf**, infrastructure. A shipped IIS-derived baseline merges honored app-root `system.webServer` collections into one validated immutable snapshot with measured add/remove/clear semantics. It feeds static content, MIME mapping, hidden segments, modules, handlers, extensions, and default documents; malformed honored sections fail activation, unhonored sections remain inert. Per-folder coverage is tenant-specific. |
| P59 | hidden segments | **portable leaf**, additive. The IIS-derived case-insensitive segment list blocks `bin` and all `App_*` directories at any depth while allowing similar names such as `App_Data_Export`; application add/remove amendments apply. `web.config` now follows the same 404 path after classic forbidden mappings retired. |
| P58 | static-file serving without IIS | **portable leaf**, additive. The managed handler restores IIS-observed validator precedence and 304 behavior; the P60 `staticContent` map supplies extension admission and content type; response spooling records validated file ranges and Kestrel commits them through `SendFileAsync`. IIS omits `Last-Modified` on its 304 while the port re-sends it, an accepted HTTP-compatible difference recorded in [compatibility](compatibility.md). Registering `OutputCacheModule`, which the portable host must do because IIS's native cache is gone, would also replay static responses: IIS served those from its native module before any managed module could store one, and a replayed entry defeats the revalidation this handler sets, since a cached entry requires every present condition to match. The handler therefore calls `SetNoServerCaching` (fenced `!NETFRAMEWORK`); the wire headers are unchanged. |
| P54 | static-file transmission | **portable leaf.** `StaticFileHandler` enters an internal already-translated send path, avoiding the insoluble Unix ambiguity between rooted virtual and physical strings. The adapter spools file ranges in order with memory writes and fails short files; public `TransmitFile`/`WriteFile` retain Framework path classification. |
| P63 | async context restoration | **portable leaf.** A neutral call-context restore hook reattaches the captured request only when a continuation resumes on its owning request thread context; synchronization-context resumes regain `HttpContext.Current`, while `ConfigureAwait(false)` stays null. Host-context publication replaces state rather than mutating captured snapshots. |
| P64 | legacy synchronization context | **unsupported.** `aspnet:UseTaskFriendlySynchronizationContext=false` selected `LegacyAspNetSynchronizationContext`, the pre-4.5 async machinery Framework itself defaulted away from at `targetFramework` 4.5+; this port supports only the task-friendly context, and activation preflight refuses the explicit false with the setting named and the change stated. The legacy class stays imported and unreached. |

| P65 | request-cookie defaults | **keep shipped 4.8.1 behavior over published-source shape.** Framework 4.8.1 applies `<httpCookies>` defaults when parsing request cookies; the pinned snapshot did not. Without the serviced behavior, reissuing a received cookie can add `SameSite=None` and lose configured security attributes. The imported path now accepts the same `aspnet:EnsureCookieDefaults` switch and defaults it on. `CookiesOverKestrelTests.Re_Issuing_A_Received_Cookie_Adds_No_Same_Site_Attribute` guards the observable difference. |
| P67 | default documents | **portable leaf**, additive. After routing, an unrouted directory rewrites to the first configured existing candidate with list casing and query/`RawUrl` preservation; slashless directories redirect 301 and empty directories return app-shaped 403. Adds prepend, matching D1-D15; malformed config follows P60 activation failure ([readings](research/iis-integration-readings.md#default-documents)). |
| P68 | `Response.Headers` off IIS | **portable leaf.** The managed collection stores headers written through it; send-time content type, cache, redirect, and cookies layer with IIS-observed precedence. After flush, adds throw and removals are inert. Boundaries: no IIS `Server` header, grouped duplicate names, and a host-specific post-flush mirror ([H1-H16](research/iis-integration-readings.md#responseheaders)). |
| P70 | directory configuration on a case-sensitive filesystem | **portable leaf.** Configuration paths are lowercased by contract, and the file is composed as `web.config`; NTFS folded both back onto the real `Guarded/Web.config` Visual Studio writes. A case-sensitive filesystem misses twice, and silently: `IsConfigRecordRequired` maps the lowercased path, finds no directory, and reports that the subtree needs no record at all, so its `<authorization>`, handlers, and settings never load and a protected page serves. The [filesystem-semantics](filesystem-semantics.md) fold now runs at the configuration system's own map-path seam (`UserMapPath.GetPhysicalPathForPath`, which feeds both `MapPath` and `GetPathConfigFilename`) and on the composed file name in `WebConfigurationHost.CombineAndValidatePath`. |
| P71 | server includes and physical/virtual classification | **portable leaf.** Physical compositions use the platform separator and case-fold from the nearest existing directory, including includes above the application root. Framework readings confirm `/`-rooted and relative inputs remain virtual; UNC stays physical; Windows-drive syntax is unavailable off Windows. Remaining FCN-only aliases are tracked by [configuration reload](follow-ups/configuration-reload-and-process-restart.md). |
| P72 | URL canonicalization and path-info | **portable leaf.** `RequestPathCanonicalizer` supplies http.sys-equivalent separator, escape, dot-segment, and above-root handling; `RequestPathInfo` applies the effective handler map. All handler-visible values in the 127-request [IIS reading](research/iis-url-canonicalization-readings.md) match; wire-only static-tier deltas remain on the [IIS-role follow-up](follow-ups/iis-role-behaviors.md). |
| P73 | directory order and wildcard `bin` loading | **portable leaf.** Enumerations use deterministic NTFS-style ordinal-ignore-case order; page batching preserves directory order. The `bin` scan matches `.DLL` case-insensitively and skips files without managed metadata while surfacing architecture and identity failures, per [readings](research/enumeration-order-and-bin-wildcard.md). Katana's OWIN scanner retains upstream order. |
| P74 | physical-separator literals | **portable leaf.** The [complete literal audit](research/backslash-literal-audit.md) fixed reached and safe compiled physical compositions; virtual/URL syntax, UNC/drive-shape checks, escaping, and Windows-only/dead code remain unchanged. LocalDb paths and application root `/` are recorded exclusions. |
| P75 | `WebProcessInformation` process name | **portable leaf.** `Environment.ProcessPath` replaces `kernel32!GetModuleFileName`; process ID already used `Environment.ProcessId`. Management events no longer fail silently off Windows. |
| P76 | request authority and server variables | **portable leaf.** Scheme, Host authority, and fixed portable values populate the Framework variable set; IIS-native TLS/topology values use documented empty or single-site shapes. Forwarded headers use ASP.NET Core middleware with its loopback-trust default; integrated rewrite variables and mutation remain unavailable. Evidence: [host adapter](research/host-adapter-residuals.md). |
| P77 | header byte encodings | **portable leaf.** Request headers decode valid UTF-8 as UTF-8 and otherwise Latin-1, matching IIS; response headers honor `<globalization responseHeaderEncoding>` after activation. Front-door responses retain host defaults. Evidence: [R4/R5](research/host-adapter-residuals.md#framework-readings). |
| P78 | long `TransmitFile`, response spill, HTTP/2 | **keep**, exercised. Long sendfile is enabled; response-spool delivery and cleanup are tested; request-body surfaces pass over h2c HTTP/2. HTTP/3 and client-certificate translation remain unassessed. WebSockets landed separately in P80. |
| P79 | mid-request `Response.Flush` | **portable leaf.** The spool commits head once and delivers new segments on each valid non-final flush; a pre-head `Response.End` flush publishes nothing. Transport failure faults the spool, aborts, and reaches the page as `HttpException`. Timing/framing boundaries follow [Framework readings](research/websockets-and-streaming.md#framework-readings). |
| P80 | WebSockets | **portable leaf**, additive. A worker-request upgrade seam maps ASP.NET Core's WebSocket feature into the imported negotiation, same-origin, state-machine, and callback surfaces. Documented deltas: post-accept body is dropped, callback `User` is null, and one integrated-only ordering check is absent. Evidence: [WebSocket readings](research/websockets-and-streaming.md#framework-readings). |
| P69 | Dynamic Data hook from `ItemType` controls | **inactive.** If `System.Web.DynamicData` is absent, the optional metadata hook is skipped rather than failing model binding. A future assembly lights it up without another seam; ordinary model binding remains exercised. |
| P66 | `ToolboxBitmapAttribute` on imported controls | **portable leaf.** A port-owned design-time marker replaces the GDI+-initializing attribute so page parsing works off Windows. The runtime no longer references `System.Drawing.Common`; see [dependency decisions](dependency-decisions.md). |
| P81 | `RolePrincipal` deserialization | **portable leaf.** Cookie payloads omit base claims state and reconstruct without modern .NET's throwing serialization constructors; general claims-bearing BinaryFormatter payloads fail explicitly instead of yielding an empty principal. Evidence: [role-cookie readings](follow-ups/forms-authentication.md#framework-readings). |
| P82 | deferred role claims | **portable leaf.** `DeferredRoleClaimsIdentity` preserves Framework's lazy role-store lookup and refresh behavior despite modern .NET removing the external-claims iterator slot; clones intentionally drop pending sequences. Evidence: [role-cookie readings](follow-ups/forms-authentication.md#framework-readings). |
| P83 | module collection | **portable leaf.** The merged `system.webServer/modules` baseline plus dynamic registrations is authoritative; classic `<httpModules>` is retired. `managedHandler` is decided once from the arriving URL, `runAllManagedModulesForAllRequests` clears it, missing/bad types fail actionably, and routing-time authorization reads the same effective list. Evidence: [module readings](research/iis-modules-handlers-readings.md). |
| P84 | `WindowsAuthenticationModule` | **registered, inert.** The baseline row remains, but Windows-mode behavior is deferred and explicit `mode="Windows"` fails activation. `FileAuthorizationModule` stays inert for non-Windows principals. |
| P85 | handler selection | **portable leaf.** The merged `system.webServer/handlers` list replaces classic `<httpHandlers>`; first matching path/verb wins, type resolution is lazy and actionable, native rows use explicit bridges, and the same walk owns mapping, path-info, and `managedHandler`. `TransferRequest` remains unsupported. Evidence: [handler readings](research/iis-modules-handlers-readings.md). |
| P86 | request-filtering extensions | **portable leaf**, additive. The IIS deny list and application add/remove/clear/allow-list semantics replace the retired classic forbidden mappings; denials now return IIS's 404. Per-folder resolution remains unimplemented. Evidence: [module readings](research/iis-modules-handlers-readings.md). |
| P87 | per-folder handlers | **portable leaf.** Immutable effective handler lists merge each folder `Web.config` with parent state using case-insensitive configuration identity; mapping, path-info, and `managedHandler` share the deepest list. Folder modules and other `system.webServer` sections remain root-only. Evidence: [MH27](research/iis-modules-handlers-readings.md). |

## Open path
