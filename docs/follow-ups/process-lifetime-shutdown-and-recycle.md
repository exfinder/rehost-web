# Process lifetime, shutdown, and recycle

Status: partially decided; full drain/disposal is slice 6. Terminal notification
is required in slice 1.

## Problem

Modern .NET cannot unload the current AppDomain. Imported shutdown paths still
expect AppDomain unload for configuration changes, explicit unload, and native
host failures. Unhandled unload attempts can break shutdown.

## Settled so far (ledger P26)

The terminal step of `HttpRuntime.ReleaseResourcesAndUnloadAppDomain` no longer
attempts an unload. On .NET 10 `AppDomain.Unload` always throws
`CannotUnloadAppDomainException`, which that method's `for (;;)` loop swallowed —
it spun a thread pool thread forever, and because no unload occurred the
`DomainUnload` notification never fired. That notification was the only caller of
`ApplicationManager.HostingEnvironmentShutdownComplete`, so `_activeHostingEnvCount`
never reached 0 and `ApplicationManager.ShutdownAll` waited out its full
`3000 × 100ms` drain before the process could exit.

Shutdown completion is now published directly by
`HostingEnvironment.CompleteShutdown()`, which runs the same sequence the
`DomainUnload` handler ran, under an interlocked guard so that either caller
reaches it exactly once. The portable parity harness exits 0 in ~0.5s.

This settles the terminal notification required in slice 1 for the graceful path
only. Still open below: configuration change, explicit unload, drain policy, and
the fact that the application does **not** restart in place — the process must be
recycled to host it again.

Exactly one live `AppDomain.Unload` call site now remains in the compiled
runtime, `Hosting/ProcessHost.cs:1240`, which is all that blocks removing the
`SYSLIB0024` suppression. Split out to
[AppDomain unload call sites and the SYSLIB0024 tripwire](appdomain-unload-call-sites.md),
which owns that cleanup and its evidence.

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
