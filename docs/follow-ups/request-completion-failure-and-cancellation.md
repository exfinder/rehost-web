# Request completion, failure, and cancellation

## Problem

System.Web completion is callback-based while ASP.NET Core awaits a task.
Failures before `EndOfRequest`, disconnects, host shutdown, or response-write
errors can otherwise hang middleware forever or lose the original exception.
Moving processing to `Task.Run` can also change execution and call context.

## Required decisions

- System.Web-owned failures complete normally after error formatting.
- Only exceptions escaping `HttpRuntime.ProcessRequest` fault completion.
- Disconnect is observable but does not abandon a pipeline-owned request.
- A pre-pipeline escape explicitly faults worker-request completion.
- ExecutionContext and logical CallContext flow across the sync/async bridge.
- Exactly-once final flush and completion.

## Verification

Focused adapter tests cover synchronous throw, asynchronous callback failure,
write failure, cancellation, duplicate completion, and missing completion.
Full-pipeline cases may defer to the integration story.

## Done when

No request can wait indefinitely after a terminal event, an escaped exception
is observed exactly once, and managed completion happens exactly once.
