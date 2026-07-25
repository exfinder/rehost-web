# Request completion, failure, and cancellation

Status: open. Priority: high. Depends on ASP.NET Core host adapter.

## Problem

System.Web completion is callback-based while ASP.NET Core awaits a task.
Failures before `EndOfRequest`, disconnects, host shutdown, or response-write
errors can otherwise hang middleware forever or lose the original exception.
Moving processing to `Task.Run` can also change execution and call context.

## Required decisions

- Single terminal state: success, handled failure, host cancellation, or fatal
  adapter failure.
- Ownership and propagation of pipeline versus transport exceptions.
- Cancellation/disconnect behavior before timeout support exists.
- ExecutionContext and logical CallContext flow across the sync/async bridge.
- Exactly-once final flush and completion.

## Verification

Focused adapter tests cover synchronous throw, asynchronous callback failure,
write failure, cancellation, duplicate completion, and missing completion.
Full-pipeline cases may defer to the integration story.

## Done when

No request can wait indefinitely after a terminal event, exceptions remain
observable, and completion/flush happen at most once.
