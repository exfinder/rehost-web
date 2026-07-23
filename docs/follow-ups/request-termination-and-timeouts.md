# Request termination and timeouts

Status: open. Priority: high. Depends on request pipeline cancellation design.

## Problem

`Response.End`, terminating redirects, and request timeouts rely on
`Thread.Abort`/`Thread.ResetAbort`, which modern .NET does not support. Failure
to unwind can strand request completion. Modern .NET also cannot safely
force-stop arbitrary synchronous user code.

## Required contract

Define behavior for:

- `Response.End` and terminating redirects;
- sync/async timeouts and blocked or CPU-bound handlers;
- pipeline/page unwinding and user `catch`/`finally`;
- `Server.Execute`/`Server.Transfer` nesting;
- timeout responses and connection abort.

Candidate: an internal control-flow exception for immediate termination plus
cooperative cancellation and pipeline checkpoints for timeouts.

## Done when

- Compatibility differences are explicit.
- Differential tests cover termination, redirects, timeouts, nesting, modules,
  and asynchronous pages.
- Unsupported abort/reset calls are removed or unreachable.
- Temporary `SYSLIB0006` suppression is removed.
