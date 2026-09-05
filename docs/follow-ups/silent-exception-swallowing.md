# Silent exception swallowing in imported Reference Source

## What the sweep covered

Imported Reference Source carries 145 empty `catch` blocks that name no
exception type (144 bare `catch {}` plus one `catch (Exception) {}`), across 65
files, concentrated in `HttpRuntime` (13), `Security/Roles` (10), and then
`UI/Page`, `Configuration/RemoteWebConfigurationHostServer`, `Hosting/HostingEnvironment`
and `Compilation/BuildResultCache` (5 each). A discarded exception during a port
is not a minor inconvenience: the pipeline slice presented as an empty `200` with
no status, no headers, and no flush, and the cause — two native dependencies and
an unresolvable `bin` assembly — was invisible until one of those blocks was
instrumented (P31).

116 of them now report on the diagnostics choke point (ledger P94). The site is
built from `nameof`, so it survives a rename and folds to a literal at compile
time; the context names the first call the `try` guarded; the exception carries
its own stack, which is what actually identifies the throwing line.

## What stays untouched, and why

- **Typed empty catches** (21). `catch (FileNotFoundException) {}` states its
  intent in the type. Instrumenting them would report expected outcomes.
- **Trivial-intent bare catches** (22). The guarded call states the intent by
  itself: `Int32.Parse` (malformed input), culture creation from a user-supplied
  name, and best-effort `File.Delete`/`Directory.Delete` during cleanup.
- **Unreachable catches** (2, both in `UI/SkinBuilder.cs`). They follow a
  `catch (Exception e)`, so no exception can reach them and the compiler rejects
  a typed rewrite.
- **Dead `#if` branches** (3) and code inside `/* */` blocks (2).

## What the sweep found

Measured over the full suite and the `apps/WebFormsApplication` smoke, all green:

- `HttpRuntime.SetThreadPoolLimits` fired once per application start, and the
  `PlatformNotSupportedException` from the native max-threads call discarded the
  portable `ThreadPool.SetMinThreads` and connection-limit work below it.
  Closed by P95; that site no longer reports.
- `CacheEntry.CallCacheItemRemovedCallback#2` fires during config-record teardown
  in the suite, never in the app smoke. `WebConfigurationHost.StopMonitoringStreamForChanges`
  dereferences a null callback list and aborts `BaseConfigurationRecord.CloseRecursive()`
  partway. Framework runs the same code, so whether this is a port defect needs a
  Framework reading before anyone calls it. Open.
- A later exception reaching `HttpRuntime.FinishRequest` after the error page has
  been rendered is dropped (P92).

## A stashed-and-masked variant, found in the page slice

Not an empty catch, but the same failure mode and worth listing beside them.

`HttpRuntime`'s instance constructor wraps its body in `catch { InitializationException = e; }`,
so a failure before `_fcm = new FileChangesMonitor(...)` leaves `_fcm` null and
the real cause stashed. `HostingInit` then calls
`StartMonitoringDirectoryRenamesAndBinDirectory()` **unconditionally**, and its
`if (InitializationException == null)` guard sits on the *next* statement. The
null `_fcm` throws, overwrites the stashed exception, and the request renders a
`NullReferenceException` from a file-monitoring method instead of the actual
initialization failure.

Reached by touching `HttpRuntime.CodegenDir` before the first request, which
constructs the singleton before hosting is initialized. Framework behaves the
same way, so this is a diagnosability defect rather than a port divergence, and
no imported source is edited for it. It cost real time to diagnose once; the note
exists so it costs less the next time.
