# ASP.NET Core host adapter

Status: first-slice envelope implemented in
[`Rehost.WebForms.Hosting`](../../src/Rehost.WebForms.Hosting), verified on
macOS `arm64` and Windows `x64`. Contract:
[first runnable request](first-runnable-request.md).

## Problem

Kestrel must enter System.Web through public
`HttpRuntime.ProcessRequest(HttpWorkerRequest)` without importing IIS hosting
assumptions or owning application lifecycle.

## Accepted decisions

- **Dependency direction.** Hosting references the runtime and never the
  reverse; the port does not know a web server exists. The only framework
  dependency is `Microsoft.AspNetCore.App`.
- **Startup versus activation.** `AddRehostWebForms` validates configuration and
  claims the process, so an unusable configuration stops the host before it
  listens. The application activates on the **first request**: activating early
  would make every served request warm and erase the cold, concurrent-cold, and
  pooled-instance claims from the gate.
- **Ownership.** `ClassicPipelineDispatcher` is a registered object created
  through `ApplicationManager`, the shape IIS uses for `ISAPIRuntime`. Middleware
  never initializes `HostingEnvironment` itself.
- **Claim rule.** `UseRehostWebForms` is terminal. Unmatched paths receive
  System.Web's own 404, which is what classic ASP.NET returns; handing them on
  would replace a response System.Web had already decided.
- **Completion.** A `TaskCompletionSource` the middleware awaits and
  `EndOfRequest` completes. An escape before the pipeline takes ownership faults
  it; once the pipeline has completed the request that fault is a no-op, which is
  what lets a System.Web-owned failure return its error page normally. A second
  `EndOfRequest` throws.
- **No deadline.** The adapter waits indefinitely. Inventing a timeout would
  fabricate a status classic ASP.NET never sends and would pre-empt
  [request termination and timeouts](request-termination-and-timeouts.md).
  Consequence: a genuinely stuck handler holds its connection until the process
  restarts.
- **Reason phrase.** Set explicitly on `IHttpResponseFeature`. Without it the
  server substitutes the standard text and every custom status description is
  lost.
- **Content length.** Held apart from the header list and assigned to
  `HttpResponse.ContentLength`, so the value System.Web calculated does not
  appear twice beside the one the server derives.
- **Absent data.** Unsupported server variables answer `null`, which System.Web
  reads as "the server does not provide this". Missing connection data answers
  empty, because a request without peer information is genuinely missing a value
  rather than being handed a fabricated one.
- **Explicit rejection.** Requests carrying an entity body are refused at
  construction; file send throws. Deriving from `HttpWorkerRequest` directly
  rather than from `SimpleWorkerRequest` is what makes this enforceable — every
  output-side member is abstract, so the compiler refuses a missed override.
  `SimpleWorkerRequest` empties all of them, including `EndOfRequest`, and would
  have discarded output silently.

## Verification

[`prototypes/adapter-parity`](../../prototypes/adapter-parity/README.md) replays
`sessions.json` over loopback HTTP, one process per session, and compares against
the same Framework golden. Unit coverage of the request mapping — encoded paths,
repeated headers, missing connection data, virtual-root containment, unsupported
members — lives in `tests/Rehost.WebForms.Hosting.Tests`.

Driving the pipeline from a real server reached one platform edge no differential
probe had: `SafeNativeMethods.GetCurrentThreadId`, recorded as P33.

## Still open

- Disconnect is observable through `IsClientConnected`, but no scenario exercises
  a mid-request disconnect.
- Response spill to disk is implemented and unexercised: no first-slice scenario
  produces a body over the memory threshold.
- Server-variable coverage is the minimum the fixture needs; IIS-only variables
  are unhandled rather than surveyed.
- `PathInfo` is always empty, and the file-path split it implies is not done.
- Request bodies, streaming, and file send: see
  [deferred request surfaces](deferred-request-surfaces.md).
