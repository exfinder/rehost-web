# Process lifetime, shutdown, and recycle

## Problem

Modern .NET cannot unload the current AppDomain. Imported shutdown paths still
expect AppDomain unload for configuration changes, explicit unload, and native
host failures. Unhandled unload attempts can break shutdown.

## Settled boundary

`ReleaseResourcesAndUnloadAppDomain` now publishes guarded shutdown completion
directly instead of attempting current-AppDomain unload (ledger P26). This
covers the graceful completion signal only; drain, explicit unload,
configuration change, and process replacement remain open.

One compiled `AppDomain.Unload` site remains, owned by
[AppDomain unload call sites and the SYSLIB0024 tripwire](appdomain-unload-call-sites.md),
which also owns removal of the project-wide suppression.

## Required contract

- Stop new dispatch and drain/terminate active requests under a defined policy.
- Dispose runtime/hosting resources exactly once.
- Emit one terminal shutdown notification to the application owner.
- ASP.NET Core calls `IHostApplicationLifetime.StopApplication`.
- Replace the process when reload or static-state reset is required.
- Define behavior without a restart-capable host.
- Make `HttpRuntime.UnloadAppDomain()` process-scoped or explicitly unsupported.
- Never call `Environment.Exit` or attempt in-process reinitialization.

## Done when

- Configuration change, explicit unload, graceful shutdown, and restart are
  specified and tested.
- Unsupported AppDomain unload calls are removed or unreachable.
- Shutdown cannot produce an unhandled ThreadPool exception.
- Temporary `SYSLIB0024` suppression is removed — owned by
  [appdomain-unload-call-sites.md](appdomain-unload-call-sites.md).
