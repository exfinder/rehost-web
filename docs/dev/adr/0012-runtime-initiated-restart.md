# Runtime-initiated restart

## Decision and rationale

One application runs per process, and modern .NET cannot unload its current
AppDomain. Runtime shutdown therefore requests exit code
RehostWebExitCodes.RestartRequested (82); a supervisor starts the replacement.
Application_Start failure follows integrated semantics: latch the error, replay
500 throughout the unchanged ten-second window and rerun start in the next process.
Classic partial-initialization continuation is unacceptable.

The app-start post-lock check runs on every call, including requests that passed
FirstRequestInit before a slow start failed. Otherwise waiters can miss the latch
and enter a partially initialized application.

Initiation direction, not shutdown cause, determines the exit code. A dispatcher
Stop caused by the runtime requests restart; a host that already claimed teardown
leaves the exit code alone so normal SIGTERM does not trigger failure restart.

The callback sets Environment.ExitCode and calls StopApplication. It never forces
Environment.Exit. Host ShutdownTimeout bounds drain; diagnostics and
ApplicationShutdownReason carry cause, while all restart causes share one code.

## Consequences

- The supervisor owns restart/backoff policy and replacement process startup.
- In-flight requests drain to the host deadline. New connections are refused
  until the replacement listens; Framework could hand them to a new AppDomain
  while the old one drained.
- Managed latch/replay stand in for integrated native failure replay without
  changing the classic execution engine.
- Configuration/file-change causes need validation and detection work in
  [configuration reload](../follow-ups/configuration-reload-and-process-restart.md).
