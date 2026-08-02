# Request entity-body bridge research

Status: implemented and verified on macOS `arm64` and Windows `x64`. Scope:
transport bytes through `HttpWorkerRequest`. Form parsing, postback, view state,
control state, and read-only multipart now sit on top of it — see
[postback and form parsing](postback-and-form-parsing.md). `HttpPostedFile.SaveAs`
is still out.

## Current seam

`AspNetCoreWorkerRequest` maps Kestrel headers, exposes disconnect through
`RequestAborted`, and delegates entity reads to an internal coordinator over
`HttpRequest.BodyReader`.
[`AspNetCoreWorkerRequest.cs`](../../src/Rehost.WebForms.Hosting/AspNetCoreWorkerRequest.cs#L21)
The accepted constraint is an asynchronous Kestrel producer serving both
synchronous and asynchronous worker-request reads, with `AllowSynchronousIO`
left disabled and no assumed eager full-body buffer.
[`ADR 0037`](../adr/0037-do-not-assume-kestrel-synchronous-io.md)

## Recovered Framework contract

- Preloaded bytes are a distinct prefix. The base worker returns no prefix;
  IIS workers expose bytes already available and report the whole entity
  preloaded only when available and total lengths match.
  [`WorkerRequest.cs`](../../src/System.Web.ReferenceSource/WorkerRequest.cs#L696),
  [`IIS7WorkerRequest.cs`](../../src/System.Web.ReferenceSource/Hosting/IIS7WorkerRequest.cs#L428)
- Classic `InputStream`, `Form`, `Files`, and `BinaryRead` first copy that prefix,
  then call synchronous `ReadEntityBody` until the declared length is consumed
  or a read returns `<= 0`. With no usable length they read until EOF.
  [`HttpRequest.cs`](../../src/System.Web.ReferenceSource/HttpRequest.cs#L948)
- `HttpRequest.ContentLength` parses the header as `Int32`; missing or invalid
  means `0`, except a wholly preloaded headerless entity reports its prefix
  length. Thus zero also represents unknown-length/chunked input.
  [`HttpRequest.cs`](../../src/System.Web.ReferenceSource/HttpRequest.cs#L1280)
- `SupportsAsyncRead` is opt-in. EOF is `EndRead == 0`; known-length callers stop
  at the declared length, while chunked callers continue to zero. IIS validates
  Stream-style arguments, completes zero-count reads synchronously, and permits
  one pending async operation.
  [`WorkerRequest.cs`](../../src/System.Web.ReferenceSource/WorkerRequest.cs#L767),
  [`IIS7WorkerRequest.cs`](../../src/System.Web.ReferenceSource/Hosting/IIS7WorkerRequest.cs#L577)
- `HttpBufferlessInputStream` serves the prefix before transport bytes, caps
  known-length reads, and treats zero length as unknown. It uses worker APM only
  outside a cancellable period; otherwise `Stream` falls back to the synchronous
  path. A connected EOF returns partial/zero; disconnected synchronous EOF
  becomes `HttpException` at this layer.
  [`HttpBufferlessInputStream.cs`](../../src/System.Web.ReferenceSource/HttpBufferlessInputStream.cs#L128),
  [`HttpBufferlessInputStream.cs`](../../src/System.Web.ReferenceSource/HttpBufferlessInputStream.cs#L217)
- IIS synchronous worker reads suppress disconnect errors to zero but throw
  other communication errors. IIS asynchronous failures become `HttpException`,
  either from `BeginRead` for an immediate failure or `EndRead` after pending I/O.
  [`IIS7WorkerRequest.cs`](../../src/System.Web.ReferenceSource/Hosting/IIS7WorkerRequest.cs#L467),
  [`IIS7WorkerRequest.cs`](../../src/System.Web.ReferenceSource/Hosting/IIS7WorkerRequest.cs#L596),
  [`IIS7WorkerRequest.cs`](../../src/System.Web.ReferenceSource/Hosting/IIS7WorkerRequest.cs#L1976)
- When async preload is enabled, System.Web uses it only if the worker supports
  async reads, a body exists, and the body is not wholly preloaded; it repeatedly
  calls `BeginRead`/`EndRead` to EOF.
  [`ImplicitAsyncPreloadModule.cs`](../../src/System.Web.ReferenceSource/ImplicitAsyncPreloadModule.cs#L39)

The imported files above are byte-for-byte from pinned Microsoft Reference
Source revision `ec9fa9ae770d522a5b5f0607898044b7478574a3`.
[`reference-source.json`](../provenance/reference-source.json)

## Transport implications

- Use Kestrel's body-presence feature, not method or header guesses. Its contract
  distinguishes definite no-body from known-length and protocol-framed bodies;
  framed bodies may still end at zero bytes.
  [`IHttpRequestBodyDetectionFeature.CanHaveBody`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.features.ihttprequestbodydetectionfeature.canhavebody?view=aspnetcore-10.0)
- The producer must use `ReadAsync`/`BodyReader`, publish ordered bytes plus one
  terminal state, and apply backpressure. Synchronous `ReadEntityBody` may block
  its System.Web thread waiting for that producer; it must never call Kestrel's
  synchronous `Request.Body.Read`. Kestrel defaults synchronous I/O off because
  blocking network I/O can starve the thread pool.
  [Kestrel synchronous-I/O guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/security-considerations?view=aspnetcore-10.0#synchronous-io)
- APM reads need a real `IAsyncResult`: accurate `AsyncState` and
  `CompletedSynchronously`, callback exactly once, one pending read, validation,
  and error observation from the matching `EndRead`. Wrapping the synchronous
  worker read in `Task.Run` does not satisfy the accepted producer contract.
- Definite no-body can report no prefix and wholly preloaded. A body-capable
  request should report no opportunistic prefix and not wholly preloaded unless
  the adapter deliberately establishes a deterministic prefix before pipeline
  entry. Racing Kestrel's currently buffered bytes would make behavior timing
  dependent.
- Transport abort/error must become one immutable terminal state. Kestrel body
  reads fail when the request is aborted; translation must preserve the
  Framework surface distinction: synchronous worker disconnect => zero,
  bufferless disconnect => `HttpException`, async failure => `HttpException`,
  other synchronous transport failure => exception.
  [`RequestAborted` guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/use-http-context?view=aspnetcore-10.0#requestaborted)
- Completion must stop any pending producer and prevent use of a recycled
  `HttpContext`; a handler is allowed to finish without consuming the whole
  request body.
- Kestrel `MaxRequestBodySize` remains host-owned and
  `httpRuntime.maxRequestLength` remains System.Web-owned. The adapter changes
  neither, does not reject differing values, and observes the smaller effective
  limit. Each layer owns its rejection and requires independent evidence.
- Kestrel raises its own limit lazily, on a read, in every case — it does not
  compare a declared `Content-Length` up front. Where that read lands is
  therefore an application property: System.Web may touch the entity before it
  dispatches, in which case the rejection aborts dispatch, or the application may
  first read inside its handler, in which case the handler has already run. The
  adapter refuses a declared length before activation so that the outcome does
  not depend on which, and an oversized declared body never starts System.Web at
  all; the response is a bare 413 with no application content type. Without a
  declared length nothing is knowable in advance and the rejection can only
  surface mid-read. A `maxRequestLength` breach stays System.Web's own uncoded
  `HttpException`, surfacing as 500 as on Framework; the adapter does not convert
  it to 413.
- A host rejection reaches System.Web as an `HttpException` carrying Kestrel's
  status code and its own message, from the synchronous read as well as the
  asynchronous one. Framework has no counterpart, because IIS rejected an
  oversized or malformed entity natively and the pipeline never ran. Returning
  zero instead — the Framework shape for a *disconnect* — would present
  truncated input to the handler as a complete body, so the handler would commit
  its side effects and only the status code would betray it. The residual
  divergence is that an application catching broadly, or one with `customErrors`
  redirecting, can still turn the rejection into its own response; that matches
  Framework's handling of any mid-request `HttpException`.
- Failure classification is by exception type, never by polling
  `RequestAborted`. The token is raised from a different path than the failing
  read, so a reset can surface first; classifying on it made the same client
  behavior latch different terminal states between runs. `ConnectionResetException`
  is a disconnect and reads as EOF, while a plain `IOException` is a transport
  failure and is preserved, matching IIS's split between disconnect HRESULTs and
  other communication errors.
- A synchronous read overlapping a pending asynchronous one throws
  `InvalidOperationException`. IIS guarded only asynchronous against
  asynchronous; its synchronous path simply blocked. The stricter guard is
  deliberate: the coordinator has no queue, so interleaving the two would reorder
  entity bytes rather than fail. System.Web itself never overlaps them.

## Required evidence

Focused worker tests cover exact/short sync reads, offset, zero-count, EOF,
deterministic preload reporting, sync/APM sequencing, second pending APM
rejection, callback and `CompletedSynchronously`, completion cancellation, and
use of an async-only request stream.

Real Kestrel HTTP/1.1 probes cover fixed-length and delayed chunked input,
`InputStream`, `BinaryRead`, buffered and bufferless reads, enabled async preload,
`Expect: 100-continue`, mid-read abort, unread-body draining on the same
connection, Framework max length and disk spill, both sides of the host limit —
declared length refused before activation, undeclared length failing the handler
mid-read — an application whose `customErrors` converts the rejection into its
own redirect, and a reset classified as EOF while `RequestAborted` is still
unraised.

The shared parity manifest includes a narrow deterministic Framework body matrix
covering known-length, chunked-equivalent, preloaded, classic, binary, buffered,
bufferless, and bufferless APM outcomes. The Framework oracle, portable runner,
adapter parity gate, and real Kestrel body scenarios pass on both supported
platforms.

This sub-slice gates HTTP/1.1. Real Kestrel HTTP/2 and HTTP/3 integration remains
an explicit slice 6 transport gate.

Application `system.webServer` request limits belong to
[`system.webServer` configuration compatibility](system-webserver-configuration-compatibility.md),
not this transport module.
