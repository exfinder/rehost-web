# Process-lifetime shutdown and recycle

## Status

Follow-up required. `SYSLIB0024` is temporarily suppressed for the Runtime
project. AppDomain unload and in-process restart remain unsupported on modern
.NET.

## Broken behavior

- `HttpRuntime` shutdown disposes runtime resources and then queues
  `AppDomain.Unload(AppDomain.CurrentDomain)`. Modern .NET throws
  `PlatformNotSupportedException`; configuration changes, hosted shutdown, and
  `HttpRuntime.UnloadAppDomain()` cannot complete their Framework lifecycle.
- `ProcessHost` unloads a custom-loader AppDomain while reporting a native-host
  error. The modern application-services overlay creates the loader in the
  current process and returns no newly created AppDomain, so there is no domain
  to unload.

Exact warning sites:

- `HttpRuntime.cs:1896`
- `Hosting/ProcessHost.cs:1240`

## Required design

The approved architecture supports one Web Forms application per process.
Define a process-lifetime boundary that:

- stops new request dispatch;
- drains or terminates outstanding requests under a documented policy;
- disposes runtime and hosting resources once;
- reports shutdown reason and desired restart to the host;
- terminates and replaces the process when reload or static-state reset is
  required;
- defines behavior when no restart-capable host is attached;
- makes `HttpRuntime.UnloadAppDomain()` explicitly process-scoped or explicitly
  unsupported.

The native `ProcessHost` custom-loader error path also needs a modern branch
that performs no AppDomain unload and preserves the original reported error.

## Completion criteria

- Define the runtime-to-host lifetime contract.
- Define configuration-change, explicit unload, graceful shutdown, and restart
  behavior.
- Remove both unsupported unload calls or prove the legacy native path
  unreachable and suppress it narrowly.
- Validate that shutdown cannot throw an unhandled ThreadPool exception.
- Remove the temporary `SYSLIB0024` suppression.
