# Process lifetime, shutdown and recycle

Modern .NET cannot unload the current AppDomain. Runtime shutdown requests
process replacement through the adapter's exit-code-82 notification; host stop
calls runtime cleanup. Current behavior is in [runtime restart](../adr/0012-runtime-initiated-restart.md).

## Open contract

- Align the host with [application lifecycle](../adr/0002-application-lifecycle.md):
  instance ownership, retriable mutation-free registration, explicit state and
  admission publication, and host-owned asset paths.
- Specify request admission, drain deadline, termination and exactly-once disposal.
- Define restart behavior for hosts without a supervisor and configuration/file
  change causes beyond the explicit-unload path.
- Remove the remaining compiled AppDomain.Unload call and its broad suppression:
  [unload call sites](appdomain-unload-call-sites.md).
- Keep terminal notification single and process-scoped; never force Environment.Exit
  or attempt in-process static reset.

## Done when

Explicit unload, configuration changes, normal stop and restart have executable
contracts; cleanup cannot produce an unhandled ThreadPool exception. Unsupported
unload calls and their temporary SYSLIB0024 suppression are removed.
