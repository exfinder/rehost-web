# Process lifetime, shutdown, and recycle

Status: partially decided; full drain/disposal is slice 6. Terminal notification
is required in slice 1.

## Problem

Modern .NET cannot unload the current AppDomain. Imported shutdown paths still
expect AppDomain unload for configuration changes, explicit unload, and native
host failures. Unhandled unload attempts can break shutdown.

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
- Temporary `SYSLIB0024` suppression is removed.
