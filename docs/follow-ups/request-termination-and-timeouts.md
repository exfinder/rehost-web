# Request termination and timeout portability

## Status

Follow-up required. `SYSLIB0006` is temporarily suppressed for the Runtime
project. Runtime behavior remains incompatible with modern .NET.

## Broken behavior

- `HttpResponse.End()` and redirects using `endResponse: true` call
  `Thread.Abort`; modern .NET throws `PlatformNotSupportedException` instead of
  unwinding the request.
- Timeout processing calls `Thread.Abort` after changing the context timeout
  state. Failure to abort can strand request completion and does not provide
  Framework-compatible timeout termination.
- Pipeline, page, legacy asynchronous-page, and ISAPI handlers call
  `Thread.ResetAbort`; modern .NET does not support the abort state they expect.

Exact warning sites:

- `RequestTimeoutManager.cs:177`
- `HttpContext.cs:1864`
- `HttpApplication.cs:2270`
- `HttpResponse.cs:3115`
- `Hosting/ISAPIRuntime.cs:192`
- `UI/Page.cs:2571`
- `UI/LegacyPageAsyncTask.cs:216`

## Required design

Define portable behavior jointly for:

- `Response.End` and terminating redirects;
- synchronous and asynchronous request timeouts;
- pipeline and page-lifecycle unwinding;
- `Server.Execute` and `Server.Transfer` nesting;
- user `catch` / `finally` blocks;
- blocked I/O and CPU-bound synchronous handlers;
- timeout response generation and connection abort.

Candidate direction: an internal ordinary control-flow exception for immediate
`Response.End` / redirect unwinding, plus cooperative cancellation and explicit
pipeline checkpoints for timeouts. Modern .NET cannot safely force-stop
arbitrary synchronous user code. Do not use `Thread.Interrupt` without a
separate compatibility decision.

## Completion criteria

- Decide and document observable compatibility differences.
- Add differential coverage for termination, redirect, timeout, nested
  execution, modules, and asynchronous pages.
- Replace all seven unsupported calls or prove a site unreachable.
- Remove the temporary `SYSLIB0006` suppression.
