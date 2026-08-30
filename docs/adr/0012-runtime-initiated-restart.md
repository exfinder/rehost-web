# Runtime-initiated restart

.NET Framework answers a runtime-initiated shutdown by unloading the AppDomain
and building a new one in the same worker process: an initialization failure,
a configuration or content change, and `HttpRuntime.UnloadAppDomain()` all end
there. Modern .NET cannot unload the current AppDomain, and this runtime keeps
one application per process, so the rebuild has no in-process form.

Measured on winbox (IIS 10, .NET Framework 4.8, 2026-08-30). Integrated mode
latches a failing `Application_Start` in `HttpRuntime.InitializationException`
for the life of the AppDomain, replays that 500 on every request, unloads the
domain on a hardcoded 10-second timer, and re-runs `Application_Start` in the
domain the next request builds — so a fault the operator clears is gone on the
next retry. Classic mode fails only the first request and then serves the rest
of the process on partial initialization, with no second `Application_Start`.

## Decision

A shutdown the runtime initiates ends the process with exit code
`WebFormsExitCodes.RestartRequested` (82), and the supervisor's replacement
process is the rebuilt application. The port takes the integrated-mode
contract for `Application_Start`: the failure latches, every request inside
the window replays it, and Classic's partial-init continue is not reproduced.
Requests parked on the app-start lock during a slow failing start also
receive the latched failure, as integrated waiters do at the
`FirstRequestInit` gate; classic's later app-start placement needs an
explicit post-lock check to match (measured: all integrated waiters get the
failure. A self-referential customErrors page caps at 302 → 500 via the
`aspxerrorpath` guard only when `defaultRedirect` carries no query string; a
query-carrying `defaultRedirect` never receives the marker the guard keys on
and loops unboundedly — identically on IIS integrated and the port, app
healthy or not, since `HttpResponse.RedirectToErrorPage` is inherited
unmodified).

The direction of initiation decides, not the cause. `ClassicPipelineDispatcher`
registers itself with the hosting environment, as `ISAPIRuntime` does, so a
runtime-initiated shutdown reaches its `Stop`; `ClassicPipelineActivation`
classifies that `Stop` by whether the host's `ApplicationStopping` had already
claimed the teardown. A host-initiated stop — SIGTERM, `StopApplication` —
leaves the exit code alone, or every clean shutdown would read as a failure
under `Restart=on-failure`.

Exit is cooperative: the callback sets `Environment.ExitCode` and calls
`IHostApplicationLifetime.StopApplication()`. No `Environment.Exit`, no
watchdog; the host's own `ShutdownTimeout` bounds the drain. One code covers
every cause, with the cause carried in the diagnostics channel and
`ApplicationShutdownReason`, because a supervisor's response to all of them is
the same. Framework's 10-second latch window is untouched: shortening it would
change how many requests see the failure, which is application-visible.

## Consequences

- Restart policy and backoff belong to the supervisor (systemd, Kubernetes,
  a process manager), which the port cannot express in-process.
- The rebuild is eager: the replacement process activates on its first
  request, where Framework rebuilds lazily inside the surviving worker
  process. In-flight requests on other connections are lost to the exit,
  which the AppDomain recycle drained instead.
- Two fenced deviations in imported source carry this: the latch in
  `HttpApplicationFactory.EnsureAppStartCalled` and the replay in
  `HttpRuntime.EnsureFirstRequestInit`, the latter standing in for the native
  module integrated mode registers for the same replay.
- Configuration-change and file-change shutdowns ride the same seam and are
  untested; they are in the [backlog](../backlog.md).
