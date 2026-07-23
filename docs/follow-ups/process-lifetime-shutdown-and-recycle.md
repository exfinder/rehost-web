# Process lifetime, shutdown, and recycle

Status: open. Priority: high. Depends on host/runtime lifecycle boundary.

## Problem

Modern .NET cannot unload the current AppDomain. Imported shutdown paths still
expect AppDomain unload for configuration changes, explicit unload, and native
host failures. Unhandled unload attempts can break shutdown.

## Required contract

- Stop new dispatch and drain/terminate active requests under a defined policy.
- Dispose runtime/hosting resources exactly once.
- Report shutdown reason and desired restart to the host.
- Replace the process when reload or static-state reset is required.
- Define behavior without a restart-capable host.
- Make `HttpRuntime.UnloadAppDomain()` process-scoped or explicitly unsupported.

## Done when

- Configuration change, explicit unload, graceful shutdown, and restart are
  specified and tested.
- Unsupported AppDomain unload calls are removed or unreachable.
- Shutdown cannot produce an unhandled ThreadPool exception.
- Temporary `SYSLIB0024` suppression is removed.
