# WebSockets and client-visible streaming over Kestrel

Evidence behind two open items: client-visible streaming (a mid-request
`Response.Flush` that reaches the wire) on the
[adapter follow-up](../follow-ups/aspnet-core-host-adapter.md), and
[WebSockets](../follow-ups/websockets.md). The boundary being amended is
[ADR 0003](../adr/0003-host-boundary.md); the current claims are the two
Unsupported rows in the [compatibility map](../compatibility.md) (WebSockets;
mid-request `Response.Flush`) and ledger P78.

Outcome (2026-08-16): streaming landed as ledger P79 (decisions 1–8 as
recommended), WebSockets as P80 (9–18 as recommended, plus a fourth imported
edit for the BeginRequest rule and "discard body after accept" for R-WS2); the
readings at the end are the evidence. Section 8 lists what needed deciding. Line
references are `file:line` against the tree at the time of writing. Statements
marked **inference** are reasoning from source, not a reading; statements marked
**reading needed** name a Windows/IIS observation the design should not guess.

Cast of files:

| role | file |
|---|---|
| adapter | `src/Rehost.WebForms.Hosting/AspNetCoreWorkerRequest.cs` |
| commit | `src/Rehost.WebForms.Hosting/RehostWebFormsMiddleware.cs`, `ResponseSpool.cs` |
| registration | `src/Rehost.WebForms.Hosting/RehostWebFormsExtensions.cs` |
| body threading precedent | `src/Rehost.WebForms.Hosting/RequestBodyCoordinator.cs`, [ADR 0006](../adr/0006-request-body-threading.md) |
| contract | `src/System.Web.ReferenceSource/WorkerRequest.cs` (`HttpWorkerRequest`) |
| response engine | `src/System.Web.ReferenceSource/HttpResponse.cs`, `HttpWriter.cs` |
| Framework reference impl | `src/System.Web.ReferenceSource/Hosting/IIS7WorkerRequest.cs` |
| WebSocket surface | `src/System.Web.ReferenceSource/HttpContext.cs`, `WebSocketPipeline.cs`, `WebSockets/*.cs` |
| pipeline | `src/System.Web.ReferenceSource/HttpApplication.cs`, `HttpRuntime.cs`, `Hosting/IPipelineRuntime.cs` |

## Result in one paragraph

Streaming is a smaller change than it looks and WebSockets is a larger one. For
streaming, System.Web already does everything right: on a non-final flush it
writes the header block, adds `Transfer-Encoding: chunked`, and emits chunk
framing into the byte stream itself (`HttpResponse.cs:740-752, 791-803`), so the
spool already holds a correctly framed streaming response — it is simply
delivered all at once at the end because `FlushResponse` is empty
(`AspNetCoreWorkerRequest.cs:330-332`). Making it stream means letting
`FlushResponse(false)` commit and push, which is one guarded state change in
`ResponseSpool` plus a decision about how a synchronous pipeline thread performs
an asynchronous Kestrel write. There is one trap: `Response.End`'s internal
flush also calls `FlushResponse(false)` **without having written headers**
(`HttpResponse.cs:703-707, 810`), so an unguarded "commit on first flush" would
publish a bare 200 with no headers; the guard is to commit only after
`SendStatus` has been seen. For WebSockets, none of Framework's plumbing is
reachable: the transition step exists only in the *integrated* step list
(`HttpApplication.cs:4023-4029` vs `ApplicationStepManager.BuildSteps` at
`:3800-3848`, which has no such step), the handoff is a native callback in
`IPipelineRuntime.cs:661-679`, `RootedObjects` — which `AcceptWebSocketRequest`
assigns the pipeline to (`HttpContext.cs:372`) — is only ever set on the
integrated path (`IPipelineRuntime.cs:517`), and `WebSocketPipe` is a p/invoke
shim over `iiswsock.dll`. What *is* reusable is the public/observable surface:
`AspNetWebSocket` is a `System.Net.WebSockets.WebSocket` over an internal
`IWebSocketPipe` (four methods, `WebSockets/IWebSocketPipe.cs:17-31`), and
`AspNetWebSocketContextImpl` already takes `(HttpContextBase, HttpWorkerRequest,
AspNetWebSocket)`. So the port can implement `IWebSocketPipe` over Kestrel's
`WebSocket` and drive the handoff from the middleware after `Completion`,
touching imported code in two places only.

---

## 1. The current commit model, precisely

### 1.1 Collection

The adapter is a pure sink. Every response-producing `HttpWorkerRequest` member
forwards into `ResponseSpool` and returns:

| member | adapter | spool |
|---|---|---|
| `SendStatus(int, string)` | `AspNetCoreWorkerRequest.cs:285-288` | `SetStatus` (`ResponseSpool.cs:51-56`) |
| `SendKnownResponseHeader` / `SendUnknownResponseHeader` | `:290-298` | `AddHeader` (`:58-62`) |
| `SendCalculatedContentLength(int/long)` | `:300-308` | `SetContentLength` (`:64-68`) |
| `SendResponseFromMemory(byte[], int)` | `:310-313` | `Write` (`:70-85`) |
| `SendResponseFromFile(string, long, long)` | `:315-318` | `WriteFile` (`:87-94`) |
| `SendResponseFromFile(IntPtr, …)` | `:322-326` | `NotSupportedException` |
| `FlushResponse(bool)` | `:330-332` | **nothing** |
| `EndOfRequest()` | `:334-344` | `Seal` + complete `_completion` |

`ResponseSpool` keeps status (default `200`/`"OK"`, `:39-41`), an ordered header
list (`:27, 47`), a separately held `ContentLength` so the commit can assign
`HttpResponse.ContentLength` instead of appending a duplicate header (`:43-45`),
and `_segments`: an ordered `List<object>` of `FileBufferingWriteStream` runs and
`FileRange` records (`:22, 26`). A memory write opens a new buffering run if none
is current (32 KiB memory threshold, unbounded spill to
`HttpRuntime.CodegenDir ?? Path.GetTempPath()`, `:20, 75-84`,
`ClassicPipelineActivation.cs:36-39`); a file write resets the current run so a
memory/file/memory sequence yields two independent runs (`:92-93`). Every
mutator calls `RequireUnsealed` (`:159-166`), which throws once `Seal` has run —
this is the "sealed after `EndOfRequest`" invariant.

### 1.2 Commit

`RehostWebFormsMiddleware.InvokeAsync` runs the pipeline inline on the ASP.NET
Core request thread and only then commits:

```
dispatcher.ProcessRequest(workerRequest);   // RehostWebFormsMiddleware.cs:48
…
await workerRequest.Completion;             // :58
await CommitAsync(context, workerRequest.Response);  // :59
```

`CommitAsync` (`:69-93`) sets `response.StatusCode`, restores the reason phrase
through `IHttpResponseFeature.ReasonPhrase` (`:76-80`), appends every spooled
header (`:82-85`), assigns `ContentLength` if one was calculated (`:87-90`), and
calls `ResponseSpool.CommitBodyAsync` (`:92`). The body commit re-validates every
file range *before the first byte leaves* — so a file that shrank fails while a
status can still be produced (`ResponseSpool.cs:109-117`) — then walks the
segments, each either `response.SendFileAsync` or
`FileBufferingWriteStream.DrainBufferAsync(response.Body, …)` (`:119-131`).
`CommitBodyAsync` refuses to run before `Seal` (`:101-107`).

`Completion` is a `TaskCompletionSource` with
`RunContinuationsAsynchronously` (`AspNetCoreWorkerRequest.cs:21-22`), set
exactly once by `EndOfRequest` (`:339-343`); a pre-pipeline escape faults it
instead (`:346-349`, `RehostWebFormsMiddleware.cs:50-56`).

### 1.3 What `FlushResponse` does today

Nothing (`AspNetCoreWorkerRequest.cs:330-332`, with the comment recording ADR
0003's reasoning). `SupportsAsyncFlush` is not overridden, so it is the base
`false` (`WorkerRequest.cs:753`), which makes `Response.SupportsAsyncFlush`
false (`HttpResponse.cs:837-841`) and degrades `BeginFlush`/`FlushAsync` to the
synchronous path (`:849-870, 897-899`). `HeadersSent()` is likewise the base
`true` (`WorkerRequest.cs:1093-1095`) and has no in-tree consumer.

### 1.4 How `HttpResponse` decides `Content-Length` vs chunked

All of this is in the private `HttpResponse.Flush(bool finalFlush, bool async)`
(`HttpResponse.cs:657-826`). Off IIS7 (`iis7WorkerRequest == null`, `:674-696`)
the shape is:

* **Deferred-`End` arm** (`:703-707`). If `_endHeadersDeferred && !finalFlush &&
  _endInternalFlush`, the header block is skipped entirely; only the buffered
  length is read. This is the port's own P55 arm — `Response.End` sends body
  bytes and leaves headers open for `EndRequest` to amend.
* **First real flush, final** (`:709-736`). `WriteHeaders()` runs, then unless
  `_contentLengthSet` or the status is 304,
  `_wr.SendCalculatedContentLength(bufferedLength)` (`:734`). That is the only
  call site of `SendCalculatedContentLength` in the tree.
* **First real flush, non-final** (`:737-753`). If no content length was set, no
  `Transfer-Encoding` was set, the status is exactly `200`, and
  `_wr.GetHttpVersion()` equals the literal `"HTTP/1.1"` (`:743`), System.Web
  appends `Transfer-Encoding: chunked` itself and sets `_chunked`. `WriteHeaders()`
  then runs — *without* any content length. Over HTTP/2 the string comparison
  fails, so neither header nor framing appears, which is accidentally correct
  because `Transfer-Encoding` is illegal there (ledger P78,
  `Http2OverKestrelTests`).
* **Content** (`:781-812`). Under `_chunked`, System.Web writes the chunk prefix
  (`Convert.ToString(len,16) + "\r\n"`), then `_httpWriter.Send(_wr)`, then
  `"\r\n"`, and on the final flush the terminator `0\r\n\r\n`, all through
  `SendResponseFromMemory` (`:791-803`). Unchunked, it is a bare
  `_httpWriter.Send(_wr)` (`:805`). Then, when not async,
  `_wr.FlushResponse(finalFlush)` (`:810`) and the buffers are cleared for a
  non-final flush (`:809, 823-824`).

`WriteHeaders` (`:596-640`) fires `PreSendRequestHeaders`, generates the block,
then calls `_wr.SendStatus`, `_wr.SetHeaderEncoding`, and one `header.Send(_wr)`
per header. **`SendStatus` therefore fires exactly when the header block is
generated, and never in the deferred-`End` arm** — this is the hook the streaming
guard needs (§2.2).

`HttpWriter.Send(wr)` (`HttpWriter.cs:1655-1668`) flushes the char buffer and
sends each `IHttpResponseElement`: memory elements via `SendResponseFromMemory`,
`HttpFileResponseElement` via `wr.SendResponseFromFile` or `wr.TransmitFile`
(`:601-609`). `ClearBuffers` (`:1352-1366`) resets the element list.

`BufferOutput = false` does not bypass any of this: it sets
`HttpWriter._responseBufferingOn` (`HttpResponse.cs:1745-1757`,
`HttpWriter.cs:954-956`) and every write path then calls `_response.Flush()`
after buffering (`HttpWriter.cs:1212-1214, 1233, 1249, 1260, 1281, 1707, 1748,
1796, 1855, 1911`) — i.e. an unbuffered response is a stream of ordinary
non-final flushes. Note also that `Response.Output.Flush()` and
`Response.OutputStream.Flush()` are **no-ops** (`HttpWriter.cs:1685-1687`, reached
from `HttpResponseStream.Flush` at `:792-794`); only `Response.Flush()`
(`HttpResponse.cs:2364-2371`) flushes.

### 1.5 What a write/flush/sleep/write page does today

Take a page that writes `"a"`, calls `Response.Flush()`, sleeps a second, writes
`"b"`, and returns. Over HTTP/1.1:

1. `Response.Flush()` → `Flush(false)`. Headers unwritten, non-final, no length,
   status 200, protocol `"HTTP/1.1"` → `Transfer-Encoding: chunked` is appended
   and `_chunked` set (`:740-750`); `WriteHeaders()` pushes status + headers into
   the spool; the chunk prefix `1\r\n`, `a`, and `\r\n` are spooled;
   `_wr.FlushResponse(false)` does nothing; buffers cleared.
2. Sleep. **Nothing is on the wire.**
3. Return → `HttpRuntime.FinishRequest` → `FinalFlushAtTheEndOfRequestProcessing`
   → `Flush(true)` (`HttpRuntime.cs:1797`, `HttpResponse.cs:832-835`). Headers
   already written, so no `SendCalculatedContentLength`; chunk prefix `1\r\n`,
   `b`, `\r\n`, then `0\r\n\r\n`.
4. `wr.EndOfRequest()` (`HttpRuntime.cs:1866-1871`) seals; the middleware
   commits.

So the answer to "computed or chunked" is **chunked** — the response is
correctly framed and carries no `Content-Length`; only its *timing* is wrong. The
client receives the whole chunked body one second late. Kestrel does not re-frame
because the application set `Transfer-Encoding` itself; that pass-through is
already pinned by `HeaderAmendmentOverKestrelTests.cs:114-126` and
`ResponseHeadersOverKestrelTests.cs:178-195`.

Over HTTP/2 the same page produces neither the header nor the framing and the
whole body arrives at the end with no length.

### 1.6 What Framework and IIS did

Same managed decisions — the code above *is* Framework's — with two differences:

* **Classic mode / `ISAPIWorkerRequest`.** Byte-identical managed output;
  `FlushResponse(false)` pushed the buffered block to the ISAPI callback and it
  went out. Headers left on the first flush, further writes streamed as chunks.
* **Integrated mode / `IIS7WorkerRequest`.** `Flush` takes the early return at
  `HttpResponse.cs:674-696`: it generates handler headers, calls
  `UpdateNativeResponse(sendHeaders: true)` to push buffers across to the native
  side, and then `iis7WorkerRequest.ExplicitFlush()` (`:683-693`,
  `IIS7WorkerRequest.cs:1989-1999`). ASP.NET does no chunking at all here —
  "IIS7 handles the chunking as necessary" (`HttpResponse.cs:681-682`) — so the
  wire framing on IIS 7+ is http.sys's, and it is a **reading needed** whether it
  matches System.Web's own hex framing byte for byte (chunk sizes per flush,
  whether small flushes coalesce).
* **Async flush.** `IIS7WorkerRequest.SupportsAsyncFlush` is `true`
  (`:525`); `BeginFlush` starts `MgdExplicitFlush(async: true)` and returns a
  `FlushAsyncResult` (`:529-559`), `EndFlush` waits and sets `_headersSent`
  (`:562-575`). `HttpResponse.BeginFlush` uses it only when
  `_wr.SupportsAsyncFlush && !_context.IsInCancellablePeriod`
  (`HttpResponse.cs:854-857`) — see §2.4, that second clause matters.

---

## 2. Streaming design options

### 2.1 The shape of the change

Commit-at-first-flush means `ResponseSpool` grows a second state. Today:
`buffering → sealed → committed`. Proposed: `buffering → streaming (status and
headers published, segments drained as they arrive) → sealed (drain the
remainder)`.

Concretely:

* `ResponseSpool` gains `CommitHeadAsync(HttpResponse, …)` (the first half of
  today's `RehostWebFormsMiddleware.CommitAsync`, `:69-91`) and
  `DrainPendingAsync(…)` (the segment walk, `ResponseSpool.cs:119-131`, resuming
  from a cursor rather than index 0).
* `RequireUnsealed` (`:159-166`) stays as-is — writes after a partial commit are
  legitimate and must append to the pending list; only `Seal` closes it.
* `SetStatus`/`AddHeader`/`SetContentLength` must additionally refuse once the
  head is committed, with the same "already sent" shape Framework used, because
  Kestrel throws on a header write after the response started. In practice
  System.Web never does this — `_headersWritten` guards every amendment
  (`HttpResponse.cs:1253, 1489, 1626, 1655, 1688, 1800, 1824, 2144, 2220-2257,
  2524, 2626, 2662`) and throws `HttpException("Server cannot append header after
  HTTP headers have been sent.")` first — so the spool guard is a defence, not a
  path.
* `RehostWebFormsMiddleware.CommitAsync` becomes "commit the head if it is not
  already committed, then drain the remainder".
* `EndOfRequest`/`Completion` are unchanged. The middleware still awaits
  `Completion` and finishes the body; only the head may have gone earlier.

### 2.2 The `Response.End` trap (must be handled, no imported change needed)

`Response.End` calls `Flush()` with `_endHeadersDeferred = _endInternalFlush =
true` (`HttpResponse.cs:3253-3262, 3275-3284`). Inside `Flush`, the deferred arm
at `:703-707` **skips the header block entirely** — no `WriteHeaders`, no
`SendStatus` — but execution still falls through to the content section and calls
`_wr.FlushResponse(false)` at `:810`. An unguarded "commit on `FlushResponse`"
would therefore publish `ResponseSpool`'s defaults (200 / `"OK"` / no headers)
and the real header block generated by the later final flush would arrive after
the response had already started — destroying P55 and every
`HeaderAmendmentOverKestrelTests` case.

The guard is exact and adapter-local: **commit the head on
`FlushResponse(finalFlush: false)` only if `SendStatus` has been called.**
`SendStatus` is reached only from `WriteHeaders` (`HttpResponse.cs:623`) and the
integrated-only `:1611`/`:3587`, so "status seen" is precisely "the header block
was generated". `ResponseSpool` already has the hook (`SetStatus`, `:51-56`); it
needs a `HasStatus` flag.

With that guard, P52/P55 keep working unchanged: `End` without a prior flush
still defers, still lets `EndRequest` amend, and still emits a `Content-Length`
computed from `_endDeferredBodyLength` (`:734`). `End` *after* a real flush is
already Framework-correct today — `_headersWritten` is true, the deferred arm is
not reachable for header purposes, `_chunked` is still set so the chunk
terminator is emitted on the final flush (`:801-803`) — it merely becomes
observable at the right time.

The one comment that goes stale is `HttpResponse.cs:3249-3252` ("Nothing reaches
the wire before the single commit on this host"), which is P55's stated
justification. Once streaming lands the sentence is false after a flush; the
*behavior* stays right for the reason Framework's did (headers were already
sent), so this is a comment/ledger edit, not a code change.

### 2.3 The threading problem

`ProcessRequest` runs inline on the ASP.NET Core request thread
(`RehostWebFormsMiddleware.cs:48`, ADR 0003, ADR 0006 "Thread-pool coupling").
`FlushResponse` is a `void` synchronous member (`WorkerRequest.cs`
`public virtual void FlushResponse(bool)`), and Kestrel's body write is
asynchronous. Kestrel's `AllowSynchronousIO` is `false` by default and ADR 0006
commits to keeping it that way, so `response.Body.Write(…)` throws.

The precedent is `RequestBodyCoordinator`, which faces the mirror-image problem
for reads and answers it three ways:

* **Synchronous `ReadEntityBody`** blocks:
  `ReadCoreAsync(...).AsTask().GetAwaiter().GetResult()`
  (`RequestBodyCoordinator.cs:46`) — explicit sync-over-async on a pooled thread,
  with the latency-cliff analysis and the two bounding properties written up in
  ADR 0006.
* **`SupportsAsyncRead` is `true`** (`AspNetCoreWorkerRequest.cs:268`) and
  `BeginRead`/`EndRead` (`:270-283`) are a real APM wrapper over the pipe
  (`RequestBodyCoordinator.cs:58-113, 156-174, 241-297`), so any System.Web
  caller that *can* be asynchronous is.
* **A fast synchronous path**: an already-buffered read completes without
  yielding (`:82-98`), which is the ordinary case.

The same three answers are available for the write side.

#### Option A — sync-over-async in `FlushResponse`

`FlushResponse(false)` does
`CommitHeadAndDrainAsync(...).GetAwaiter().GetResult()`. Symmetric with
`RequestBodyCoordinator.Read` and requires no imported change, so `Response.Flush()`,
`BufferOutput = false`, and `Response.FlushAsync()` inside a synchronous page all
stream.

Cost: the pipeline thread blocks for the duration of a socket write. This is
strictly better than the read case — a write to Kestrel's output pipe completes
without I/O unless backpressure applies (the pipe has a pause threshold), so the
common flush does not yield at all; the blocking case is a client too slow to
drain, which Kestrel's `MinResponseDataRate` (240 bytes/s after a 5 s grace, on
by default) eventually aborts. Deadlock risk is the ADR 0006 one: the completion
that releases the thread comes from the same pool.

A refinement worth measuring: write through `response.BodyWriter` (a `PipeWriter`)
rather than `response.Body`. `PipeWriter.Write` is a synchronous copy and only
`FlushAsync` can pend, so the `GetAwaiter().GetResult()` is over an operation that
usually completes synchronously. This does not remove the hazard, it shrinks it.

#### Option B — `SupportsAsyncFlush = true` plus a real `BeginFlush`/`EndFlush`

Override `SupportsAsyncFlush` (`WorkerRequest.cs:753`) and implement
`BeginFlush`/`EndFlush` (`:758-765`) as a genuine APM wrapper, exactly as
`BeginRead`/`EndRead` already are. `HttpResponse.BeginFlush` then takes
`Flush(false, async: true)` — which writes headers and spools content but does
**not** call `FlushResponse` (`HttpResponse.cs:807-811` is guarded by `!async`)
— and hands the push to `_wr.BeginFlush` (`:854-857`); `EndFlush` sets
`_headersWritten`, clears the writer buffers for a non-IIS7 worker request, and
completes (`:872-886`). No thread blocks.

The catch is the second clause of the guard: `!_context.IsInCancellablePeriod`
(`:854, 874`). A synchronous handler runs inside a cancellable period —
`ExecuteStep` opens one for any step whose `IsCancellable` is true
(`HttpApplication.cs:2198-2207`, `HttpContext.cs:1754-1783`) and
`CallHandlerExecutionStep.IsCancellable` is true unless the handler is an
`IHttpAsyncHandler` (`HttpApplication.cs:3617-3620`). So:

* In an **async page or async handler**, `Response.FlushAsync()` takes the
  asynchronous path and nothing blocks.
* In an **ordinary synchronous page**, `Response.FlushAsync()` falls back to the
  synchronous arm (`:860-869`), and plain `Response.Flush()` never even consults
  `SupportsAsyncFlush` — it is `Flush(false)` → `_wr.FlushResponse(false)`
  (`:2364-2371`, `:810`), unconditionally.

Option B therefore **cannot stand alone**: it makes the modern async surface
correct and leaves `Response.Flush()` — which is what a Web Forms progress page
actually calls — with nothing to do. It composes with A rather than replacing it.

#### Option C — enable synchronous I/O for the request

Set `IHttpBodyControlFeature.AllowSynchronousIO = true` in the middleware and let
`response.Body.Write` block. This contradicts ADR 0003 ("Kestrel synchronous I/O
stays disabled") and ADR 0006, buys nothing over A (the blocking is the same,
just inside Kestrel), and loses the `PipeWriter` refinement. Not recommended;
recorded so the option is closed explicitly.

#### Recommendation

A + B: sync-over-async through `BodyWriter` in `FlushResponse` for the classic
surface, and a real `SupportsAsyncFlush`/`BeginFlush`/`EndFlush` so async pages
and handlers never block. Both are adapter-only. Record the thread-occupancy
consequence as an amendment to ADR 0006 rather than a new ADR.

### 2.4 Member-by-member trace

What calls what, off IIS7, once streaming is in:

| application call | `HttpResponse` | `HttpWriter` | worker request |
|---|---|---|---|
| `Response.Flush()` | `:2364-2371` → `Flush(false)` | `GetBufferedLength`, `Filter`, `Send` | `SendStatus`, `Set*ResponseHeader`, `SendResponseFromMemory`×n, **`FlushResponse(false)`** |
| `Response.Write` with `BufferOutput=false` | `HttpWriter.cs:1212` → `_response.Flush()` | as above | as above |
| `Response.FlushAsync()` / `BeginFlush` (async handler) | `:849-870` → `Flush(false, true)` then `_wr.BeginFlush` | `Send` | `SendStatus`, headers, `SendResponseFromMemory`×n, **`BeginFlush`** |
| `EndFlush` | `:872-886` | `ClearBuffers` | **`EndFlush`** |
| `Response.End()` | `:3243-3291` → `Flush()` with `_endHeadersDeferred` | `Send` | `SendResponseFromMemory`×n, `FlushResponse(false)` **with no `SendStatus`** |
| `Response.TransmitFile` / `WriteFile` | — | `:1241-1262` adds `HttpFileResponseElement`; flushes if unbuffered | `SendResponseFromFile(string, long, long)` |
| end of request | `HttpRuntime.cs:1797` → `Flush(true)` | `Send` | `SendCalculatedContentLength` (first flush only), `SendResponseFromMemory`, `FlushResponse(true)`, then `EndOfRequest` |
| `Response.Close()` | `:2044-2050` | — | `CloseConnection` (adapter: not overridden) |

### 2.5 Interactions

* **Spill.** A `FileBufferingWriteStream` run that has already been drained must
  not be drained twice; a run still open when a later flush arrives must be
  drained and then either continued or replaced. Simplest correct rule: on every
  flush, close the current run, drain every not-yet-drained segment in order, and
  start a fresh run for subsequent writes. Streaming also makes spill *rarer* —
  a flushing page never accumulates 32 KiB — but the spill tests (`ResponseSpoolTests`,
  ledger P78) must still cover the interleaved case.
* **sendfile.** `SendFileAsync` after the response has started is fine and is how
  a `TransmitFile` following a flush would go out. Range revalidation
  (`ResponseSpool.cs:109-117`) currently happens once, before the first byte; with
  streaming it must happen per segment as that segment is reached, and a failure
  mid-body can no longer produce a status. That is the honest behavior — it is what
  IIS did too — but it needs to fail loudly rather than truncate silently.
* **Headers after a flush.** Already correct and already tested: System.Web
  throws `HttpException("Server cannot append header after HTTP headers have been
  sent.")` from the `_headersWritten` guards, pinned by
  `ResponseHeadersOverKestrelTests.cs:178-195` and
  `HeaderAmendmentOverKestrelTests.cs:114-126`. Streaming does not change the
  managed decision, only makes the underlying claim true.
* **Exceptions after a flush.** `HttpRuntime.FinishRequest` → `ReportRuntimeError`
  (`HttpResponse.cs:1459-1556`) already branches on `_headersWritten`: when
  headers are out it does **not** touch the status, it `Clear()`s the buffer,
  emits the closing-tag salvage block, and appends the error message into the
  open body (`:1543-1555`). So a 500 body cannot and does not replace an
  already-sent 200 — Framework's own answer. The adapter must not try to be
  cleverer: once the head is committed, a commit-path failure can only abort the
  connection.
* **`Response.End` after a flush.** Covered in §2.2: the deferred arm is inert
  because `_headersWritten` is true; the chunk terminator still goes out on the
  final flush.
* **`IsClientConnected`.** `HttpResponse.IsClientConnected` latches
  `_clientDisconnected` from `_wr.IsClientConnected()` (`:1951-1963`) and `Flush`
  skips both the header block and the content when it is set (`:710, 783`). The
  adapter answers from `RequestAborted` plus the body coordinator's latched
  terminal (`AspNetCoreWorkerRequest.cs:233-236`), which is the right source; a
  streaming write that fails because the peer went away should latch the same
  disconnect so the next `Flush` no-ops instead of throwing repeatedly.
  Framework's `FlushCore` threw `HttpException(ClientDisconnected)` on a failed
  push, so an adapter `FlushResponse` that throws `HttpException` on an aborted
  request is shape-correct (**reading needed** for the exact message/behavior at
  R-S4 below).
* **HTTP/2 and HTTP/3.** Nothing to do: the chunk framing is gated on the
  `"HTTP/1.1"` string (`:743`), so h2 streams unframed and Kestrel handles
  DATA-frame delivery. `Http2OverKestrelTests` should gain a streaming case.

---

## 3. WebSockets: what ASP.NET does end to end on IIS

### 3.1 Gate

`HttpContext.GetWebSocketInitStatus` (`HttpContext.cs:182-206`) returns, in order:

1. `RequiresIntegratedMode` if `_wr as IIS7WorkerRequest` is null (`:183-187`) —
   this is the port's current refusal, pinned by
   `tests/Rehost.WebForms.Runtime.Tests/HttpContextWebSocketsTests.cs`.
2. `CannotCallFromBeginRequest` if `CurrentNotification <= BeginRequest`.
3. `NotAWebSocketRequest` / `NativeModuleNotEnabled` from two server variables
   that `iiswsock.dll` publishes: `IIS_WEBSOCK == "websockets"` and
   `WEBSOCKET_VERSION != null` (`IIS7WorkerRequest.cs:2589-2597`).
4. `CurrentRequestIsChildRequest` for a child request.

`IsWebSocketRequest` (`:208-231`) converts 1 → `PlatformNotSupportedException`,
2 → `InvalidOperationException`, `Success` → true, everything else → false.

### 3.2 Accept

`AcceptWebSocketRequest(userFunc, options)` (`:279-372`):

* null check; refuse a second call via `IsWebSocketRequestUpgrading` (`:294-298`);
* `SynchronizationContextUtil.ValidateModeForWebSockets()` (`:304`) — requires
  `SynchronizationContextMode.Normal`, i.e.
  `AppSettings.UseTaskFriendlySynchronizationContext` true
  (`Util/SynchronizationContextUtil.cs:13-17, 65-70`);
* the init-status switch (`:306-372`), each arm with its own exception type;
* `CurrentNotification > ExecuteRequestHandler` → too late (`:318-321`);
* `options.RequireSameOrigin` → `WebSocketUtil.IsSameOriginRequest(wr)`
  (`:338-343`, `WebSockets/WebSocketUtil.cs:19-45`) — already worker-request
  generic, no IIS dependency;
* subprotocol validation against `WebSocketRequestedProtocols`, which reads the
  `Sec-WebSocket-Protocol` request header through `GetUnknownRequestHeader`
  (`:252-270, 346-359`);
* `wr.AcceptWebSocket()` → `IIS.MgdAcceptWebSocket`
  (`IIS7WorkerRequest.cs:2599-2602`);
* transition `Inactive → AcceptWebSocketRequestCalled` (`:364`);
* `Response.StatusCode = 101`, and if a subprotocol was negotiated,
  `Response.AppendHeader("Sec-WebSocket-Protocol", …)` plus
  `_webSocketNegotiatedProtocol` (`:366-370`);
* `RootedObjects.WebSocketPipeline = new WebSocketPipeline(RootedObjects, this,
  userFunc, subprotocol)` (`:372`).

### 3.3 Handoff

* An implicit **post-`EndRequest`** step, `TransitionToWebSocketsExecutionStep`,
  is registered in `PipelineStepManager.BuildSteps` (`HttpApplication.cs:4023-4029`).
  It no-ops unless `RootedObjects.WebSocketPipeline != null` and the status is
  still 101 (`:3632-3640`); otherwise it stores the response cookies on the
  request (`:3643`), transitions `AcceptWebSocketRequestCalled → TransitionStarted`
  (`:3650`) and reports `CompletedSynchronously = false` so the pipeline unwinds
  (`:3651`).
* `IPipelineRuntime.ProcessRequestNotification` sees
  `HasWebSocketRequestTransitionStarted && status == Pending`, checks that this is
  the thread that started the transition, releases the `HttpContext`, and calls
  `root.WebSocketPipeline.ProcessRequest()` (`Hosting/IPipelineRuntime.cs:661-679`).
* `WebSocketPipeline.ProcessRequest` (`WebSocketPipeline.cs:41-52`) starts the
  async body, then chains `AbortAsync` and `MgdPostCompletion` so IIS's state
  machine only advances once all pending I/O is done.
* `ProcessRequestImplAsync` (`:68-157`) suppresses further send-response
  notifications, does `FlushResponse(true)` + `ExplicitFlush()` — **this is what
  puts the 101 on the wire** (`:54-66, 75-82`) — fetches the native
  `UnmanagedWebSocketContext`, wraps it in a `WebSocketPipe`, constructs
  `AspNetWebSocket(pipe, subProtocol)` (`:87-89`), calls
  `_httpContext.CompleteTransitionToWebSocket()` (`:92`, →
  `HttpContext.cs:2242-2247` → `ClearReferencesForWebSocketProcessing` +
  `TransitionStarted → TransitionCompleted`), installs a fresh
  `AspNetSynchronizationContext` (`:95-96`), registers the socket with
  `AspNetWebSocketManager.Current` (`:101`), and invokes the user delegate via
  `syncContext.Send` with a new
  `AspNetWebSocketContextImpl(new HttpContextWrapper(_httpContext),
  _root.WorkerRequest, webSocket)` (`:109-111`). It awaits the returned task,
  then disposes the socket and deregisters it (`:140-149`).
* Frames flow `AspNetWebSocket.{ReceiveAsync,SendAsync,CloseAsync,
  CloseOutputAsync,Abort}` → `IWebSocketPipe.{ReadFragmentAsync,
  WriteFragmentAsync,WriteCloseFragmentAsync,CloseTcpConnection}`
  (`WebSockets/AspNetWebSocket.cs:368-400, 430-465, 221-333, 160-199`;
  `IWebSocketPipe.cs:17-31`) → `iiswsock.dll` through
  `UnmanagedWebSocketContext` (`WebSocketPipe.cs:39-220`).

### 3.4 What the callback can see

`AspNetWebSocketContext` (`WebSockets/AspNetWebSocketContext.cs:21-208`) is an
abstract `WebSocketContext` whose members are all `virtual`/`override` throwing
`NotImplementedException`; `AspNetWebSocketContextImpl` overrides every one
(`AspNetWebSocketContextImpl.cs:20-280`). Notably:

* `public override WebSocket WebSocket => _webSocket;`
  (`AspNetWebSocketContext.cs:204-207`, `AspNetWebSocketContextImpl.cs:275-279`)
  — the property type is `System.Net.WebSockets.WebSocket`, the backing field is
  `AspNetWebSocket` (`AspNetWebSocketContextImpl.cs:28`).
* There is **no `Session` member at all**, and after the transition
  `HttpContext.Session` returns `null` (`HttpContext.cs:1009-1013`) and
  `HttpContext.Response` throws `HttpException` (`:935-940`). `Items` survives —
  it is deliberately not cleared once the transition has started
  (`:2232-2238`) and is exposed as `AspNetWebSocketContext.Items`.
* `HttpContext.Current` **is** available inside the callback: `ISyncContext.Enter`
  builds a `ThreadContext(_httpContext)` and associates it
  (`WebSocketPipeline.cs:167-172`), so the slimmed context is current.

---

## 4. Mapping to Kestrel

### 4.1 The transport

Kestrel exposes `IHttpWebSocketFeature` with `bool IsWebSocketRequest` and
`Task<WebSocket> AcceptAsync(WebSocketAcceptContext)` (ASP.NET Core 10 ref pack,
`Microsoft.AspNetCore.Http.Features`). `AcceptAsync` writes the 101 including
`Sec-WebSocket-Accept` and, from `WebSocketAcceptContext.SubProtocol`, the
`Sec-WebSocket-Protocol` response header, then returns a
`System.Net.WebSockets.WebSocket`.

**The feature is not present by default.** Kestrel supplies `IHttpUpgradeFeature`;
`IHttpWebSocketFeature` comes from `WebSocketMiddleware`
(`Microsoft.AspNetCore.Builder.WebSocketMiddlewareExtensions.UseWebSockets`).
So `UseRehostWebForms` must register `UseWebSockets()` ahead of the terminal
middleware, in the same spirit as the `ForwardedHeaders` registration it already
performs (`RehostWebFormsExtensions.cs:56-63`).

### 4.2 Where the pipeline ends and the upgrade begins

The classic pipeline has no equivalent of the integrated handoff, and building
one inside `HttpRuntime`/`HttpApplication` would mean editing the step list, the
step manager, and `FinishRequest`. It is not necessary. The natural seam is the
one the middleware already owns:

```
dispatcher.ProcessRequest(workerRequest);   // runs to EndRequest, final flush,
await workerRequest.Completion;             // EndOfRequest → spool sealed
if (workerRequest.WebSocketHandoff is { } handoff)
    await UpgradeAsync(context, workerRequest.Response, handoff);
else
    await CommitAsync(context, workerRequest.Response);
```

`UpgradeAsync` copies the spooled status-relevant headers onto
`context.Response` (dropping `Content-Length` — the final flush will have called
`SendCalculatedContentLength(0)` at `HttpResponse.cs:734`, illegal on a 101 —
and dropping `Sec-WebSocket-Protocol`, which `AcceptAsync` writes itself), calls
`context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext { SubProtocol
= handoff.SubProtocol })`, wraps the returned `WebSocket` in the port's
`IWebSocketPipe`, constructs `AspNetWebSocket`, drives
`CompleteTransitionToWebSocket` + sync-context installation + manager
registration the way `WebSocketPipeline.ProcessRequestImplAsync` does, awaits the
user delegate, then aborts/disposes. The connection stays open because the
middleware has not returned; ASP.NET Core keeps the request alive for exactly as
long as the callback runs. Any spooled *body* bytes are discarded — a 101 has no
entity — which is a **reading needed** (R-WS3: what IIS did with bytes an
`EndRequest` module wrote after a 101).

This keeps `Completion` and `EndOfRequest` exactly-once and unchanged, and keeps
the single-commit contract intact: on an upgraded request there simply is no
commit, there is a handshake.

### 4.3 The pipe adapter

`IWebSocketPipe` is four methods (`WebSockets/IWebSocketPipe.cs:17-31`) and maps
essentially 1:1 onto `System.Net.WebSockets.WebSocket`:

| `IWebSocketPipe` | Kestrel `WebSocket` |
|---|---|
| `Task<WebSocketReceiveResult> ReadFragmentAsync(ArraySegment<byte>)` | `ReceiveAsync(buffer, CancellationToken.None)` — same return type, close frames already carry `CloseStatus`/`CloseStatusDescription` |
| `Task WriteFragmentAsync(ArraySegment<byte>, bool isUtf8Encoded, bool isFinalFragment)` | `SendAsync(buffer, isUtf8 ? Text : Binary, isFinalFragment, …)` |
| `Task WriteCloseFragmentAsync(WebSocketCloseStatus, string)` | `CloseOutputAsync(status, description, …)` |
| `void CloseTcpConnection()` | `Abort()`, plus `context.Features.Get<IConnectionLifetimeFeature>()?.Abort()` for the rude TCP close Framework's name implies |

The interface carries `[SecurityPermission(LinkDemand, UnmanagedCode = true)]`
attributes, which are inert on this runtime. `AspNetWebSocket`'s constructor is
`internal` (`AspNetWebSocket.cs:102`) and `IWebSocketPipe` is internal, but
`Rehost.WebForms.Runtime`'s internals are already visible to
`Rehost.WebForms.Hosting` (`src/Rehost.WebForms.Runtime/InternalsVisibleTo.cs:5`),
so the adapter can live in the hosting assembly with no new seam. Passing a null
pipe puts the socket straight into `Aborted` (`:109-112`), which is the right
answer when the handshake fails.

The alternative — bypass `AspNetWebSocket` and hand the callback a context whose
`WebSocket` is Kestrel's directly — is possible because
`AspNetWebSocketContext.WebSocket` is typed `System.Net.WebSockets.WebSocket`
(`AspNetWebSocketContext.cs:204-207`) and every member of the abstract class is
`virtual`, so a port-owned subclass is legal. It costs: `AspNetWebSocket`'s state
machine and `Abort`/`AbortAsync` contract, `AspNetWebSocketManager` accounting
and therefore `AspNetWebSocketContext.ConnectionCount`
(`AspNetWebSocketContext.cs:51-53`, `AspNetWebSocketManager.cs:29-84`), and any
application code that casts to `AspNetWebSocket`. It is also a *wider* imported
change if `AspNetWebSocketContextImpl` is reused, since its field is typed
`AspNetWebSocket` (`AspNetWebSocketContextImpl.cs:28, 32`). Recommend the pipe
adapter.

### 4.4 `IsWebSocketRequest` and the config gates

* `IsWebSocketRequest` should read `IHttpWebSocketFeature.IsWebSocketRequest`
  (i.e. `context.WebSockets.IsWebSocketRequest`), which is the `Upgrade:
  websocket` handshake check, replacing the `IIS_WEBSOCK`/`WEBSOCKET_VERSION`
  server-variable pair. `NativeModuleNotEnabled` maps to "the feature is absent"
  — i.e. `UseWebSockets()` was not registered — and should keep Framework's
  `WebSockets_WebSocketModuleNotEnabled` message rather than invent one.
* The `CannotCallFromBeginRequest` ordering rule (`HttpContext.cs:188-190`) keys
  on `CurrentNotification`, which the classic pipeline does not maintain.
  Preserving the rule needs a classic equivalent (the pipeline stage) or a
  deliberate decision to drop it.
* `targetFramework >= 4.5` is **already enforced** by the port at activation:
  `ApplicationManager` refuses to activate an application whose `<httpRuntime
  targetFramework>` is absent or below 4.5
  (`Hosting/ApplicationManager.cs:1364-1374`). It then publishes the
  `FrameworkName` into `ASPNET_TARGETFRAMEWORK` (`:1376`), which
  `BinaryCompatibility` reads (`Util/BinaryCompatibility.cs:22-26`), so
  `TargetsAtLeastFramework45` is true and
  `AppSettings.UseTaskFriendlySynchronizationContext` defaults to `true`
  (`Util/AppSettings.cs:69-71`). Therefore
  `SynchronizationContextUtil.ValidateModeForWebSockets()` (`:65-70`) **passes on
  this port by construction**, unless an application explicitly sets
  `<add key="aspnet:UseTaskFriendlySynchronizationContext" value="false" />`, in
  which case Framework's own `InvalidOperationException` with the
  remove-the-switch guidance is the correct outcome and needs no change.
* There is no `HttpRuntime.EnableWebSockets` in the tree; the "enabled" gate was
  the native module's presence, which §4.4's first bullet replaces.
* `WebSocketTransitionState` (`WebSocketTransitionState.cs`) and
  `IsWebSocketRequestUpgrading`/`HasWebSocketRequestTransition*`
  (`HttpContext.cs:236-247`) are transport-neutral and reusable as-is, provided
  the middleware drives the same `Inactive → AcceptWebSocketRequestCalled →
  TransitionStarted → TransitionCompleted` order. The many
  `EnsureHasNotTransitionedToWebSocket` guards across `HttpContext`,
  `HttpRequest`, and `httpserverutility` then work unchanged.
* `WebSocketNegotiatedProtocol` (`HttpContext.cs:274-277`) is set by
  `AcceptWebSocketRequest` and is what the middleware reads to fill
  `WebSocketAcceptContext.SubProtocol`.
* Session and `Response` behave exactly as on Framework because the behavior is
  in imported code the port keeps: `Session` returns `null`
  (`HttpContext.cs:1009-1013`), `Response` throws (`:935-940`), `Items` survives
  (`:2232-2238`), and `HttpContext.Current` is present through the installed
  sync context (`WebSocketPipeline.cs:167-172`).

### 4.5 Imported-code changes, with justification

The project requires each Reference Source edit to be justified. The minimum set:

1. **`WorkerRequest.cs` — three new `internal virtual` members**
   (`SupportsWebSockets => false`, `IsWebSocketRequest() => false`,
   `AcceptWebSocket(Func<AspNetWebSocketContext, Task>, string)` throwing).
   *Justification:* `HttpContext` reaches WebSocket capability only by casting to
   `IIS7WorkerRequest` (`HttpContext.cs:183, 361`), which no portable host can
   be. This is the same host-neutral-seam shape ADR 0003 already uses for
   `SupportsAsyncRead`/`BeginRead` and `SupportsLongTransmitFile`. Adding to the
   contract is additive and cannot change Framework behavior, since the defaults
   reproduce today's refusal exactly.
2. **`HttpContext.GetWebSocketInitStatus` (`:182-206`)** — consult
   `_wr.SupportsWebSockets` / `_wr.IsWebSocketRequest()` before the
   `IIS7WorkerRequest` cast, keeping every existing status code and message.
   *Justification:* this method *is* the gate; there is no way to enable the
   feature without it, and the alternative (a port-owned parallel
   `AcceptWebSocketRequest`) would duplicate the argument checking, the
   same-origin check, the subprotocol negotiation, and the state machine.
3. **`HttpContext.AcceptWebSocketRequest` tail (`:361-372`)** — for a non-IIS7
   worker request, call `_wr.AcceptWebSocket(userFunc, subprotocol)` instead of
   `wr.AcceptWebSocket()` + `RootedObjects.WebSocketPipeline = …`.
   *Justification:* `RootedObjects` is assigned only on the integrated path
   (`Hosting/IPipelineRuntime.cs:517`), so line 372 would null-reference on this
   host; `WebSocketPipeline`'s constructor requires it, and its `ProcessRequest`
   is three IIS p/invokes deep (`WebSocketPipeline.cs:51, 59-60, 73, 87`). The
   surrounding validation is worth keeping verbatim.

Everything else is port-owned adapter code in `Rehost.WebForms.Hosting`: the
`IWebSocketPipe` implementation over Kestrel's `WebSocket`, the handoff in the
middleware, the `UseWebSockets()` registration, and the transition/manager
sequencing transcribed from `WebSocketPipeline.ProcessRequestImplAsync`.

Explicitly **not** changed: `HttpApplication`'s step lists,
`ApplicationStepManager`, `HttpRuntime.ProcessRequestInternal`/`FinishRequest`,
`IPipelineRuntime`, `WebSocketPipeline.cs`, `WebSocketPipe.cs`,
`UnmanagedWebSocketContext.cs`, `AspNetWebSocket.cs`,
`AspNetWebSocketContext*.cs`. If `AspNetWebSocketContextImpl` turns out to need
a `WebSocket`-typed field (only under the bypass design of §4.3), that becomes a
fourth edit and should be surfaced as a decision, not documented afterwards.

---

## 5. Test plan

### 5.1 Streaming

**Unit (adapter, no Kestrel).** `AspNetCoreWorkerRequestTests` and
`ResponseSpoolTests` already drive a fake `HttpContext`
(`AspNetCoreWorkerRequestTests.cs:548-700`, `ResponseSpoolTests.cs`). Add:

* `FlushResponse(false)` **before** any `SendStatus` commits nothing — the
  `Response.End` trap of §2.2, asserted directly rather than through a page.
* `FlushResponse(false)` after `SendStatus` publishes status, reason phrase, and
  headers, and drains the segments written so far; a later `SendResponseFromMemory`
  + `FlushResponse(false)` drains only the new bytes (no double-send).
* `SetStatus`/`AddHeader` after the head is committed fail loudly.
* A file segment before and after a flush: `SendFileAsync` ordering preserved,
  the shrunk-file failure (`ResponseSpool.cs:142-157`) still detected at the
  segment it belongs to.
* `SupportsAsyncFlush` true and `BeginFlush`/`EndFlush` round-trip, including the
  completed-synchronously path, mirroring the `BeginRead`/`EndRead` tests.

**Kestrel scenario (the red test that opens the slice).** A `page` fixture page
that writes, `Response.Flush()`es, waits ~1 s, writes again, and returns; read
with `RawSocketProbe` (`Harness/RawSocketProbe.cs:40-66`, which already returns
raw bytes so no client layer re-frames the answer). Assert, with a stopwatch
started before the request:

* the status line and header block arrive **before** the delay elapses;
* the header block contains `Transfer-Encoding: chunked` and no `Content-Length`;
* the first chunk's bytes arrive before the delay elapses and the second after;
* the full de-chunked body is exact.

`RawSocketProbe` needs one addition: an incremental reader that timestamps
arrivals rather than `CopyToAsync` to completion. `DelayedChunkedContent`
(`Harness/DelayedChunkedContent.cs`) is the request-side analogue and is the
model for the shape.

**Regression surfaces that must stay green.** `ResponseEndOverKestrelTests` (all
five), `HeaderAmendmentOverKestrelTests` (especially
`A_Flush_After_End_In_EndRequest_Seals_And_Forfeits_The_Length` at `:98-112` and
`An_Application_Flush_Still_Seals_The_Headers` at `:115-126`),
`ResponseHeadersOverKestrelTests.H15`, `ResponseSpoolTests`,
`StaticFilesOverKestrelTests`, `Http2OverKestrelTests`. Add an h2 streaming case
asserting no `Transfer-Encoding` and delivery still incremental.

**Also worth covering:** `Response.BufferOutput = false` (a stream of implicit
flushes), an exception thrown after a flush (status stays 200, error text appended
after the salvage block — `HttpResponse.cs:1543-1555`), a client that disconnects
mid-stream (`IsClientConnected` false, no unhandled exception, temp files
cleaned), and a `TransmitFile` after a flush.

### 5.2 WebSockets

**Unit.** A fake `HttpContext` carrying a stub `IHttpWebSocketFeature`:
`IsWebSocketRequest` true/false, `AcceptAsync` returning a fake `WebSocket`,
`AcceptAsync` throwing. Assert the adapter's `IsWebSocketRequest`, the handoff
publication, the header filtering (`Content-Length` and
`Sec-WebSocket-Protocol` dropped), and the pipe adapter's four-method mapping
against a scripted `WebSocket` double.

**Kestrel scenario.** A `ScenarioProbes` handler that calls
`context.AcceptWebSocketRequest(async ctx => { echo frames until Close; then
CloseAsync; })`, registered in a new `websocket` fixture's `<httpHandlers>` the
way `RequestBodyHandler` is (`fixtures/body/web.config:13-35`). Drive it with
`System.Net.WebSockets.ClientWebSocket`:

* text frame echo, binary frame echo, a fragmented message;
* subprotocol negotiation: client offers `a, b`, `AspNetWebSocketOptions.SubProtocol
  = "b"` → the 101 carries `Sec-WebSocket-Protocol: b` and
  `AspNetWebSocketContext.WebSocket.SubProtocol` is `"b"`; offering a protocol the
  client did not list throws `ArgumentException` (`HttpContext.cs:352-358`);
* clean close initiated by each side; `CloseStatus`/`CloseStatusDescription`
  round-trip;
* `RequireSameOrigin` accepted and refused (403, `:338-343`);
* the callback's context: `Items` visible, `Session` null, `Response` throws,
  `HttpContext.Current` non-null, `AspNetWebSocketContext.ConnectionCount` 1
  during and 0 after;
* `AcceptWebSocketRequest` on a non-upgrade request → `HttpException` 400;
  called twice → `InvalidOperationException`;
* `UseWebSockets()` absent → Framework's module-not-enabled message.
* Flip `HttpContextWebSocketsTests` to the working path for a host that supports
  it, keeping a `SimpleWorkerRequest` case for the still-correct refusal.

Three platforms, per the follow-up's "done when".

### 5.3 Readings to take on IIS + Framework 4.8

The wire rig on `win-oracle` (`eng/wire-rig`, `docs/windows-validation-host.md:132-175`)
answers the streaming questions; the WebSocket questions need the same box with
the `Web-WebSockets` IIS feature added (full IIS is available on winbox since
2026-08-16).

| id | question |
|---|---|
| R-S1 | Headers on the first flush of a write/flush/delay/write page: exact header set and order, `Transfer-Encoding` vs `Content-Length`, whether IIS emits `Content-Length` when it can infer one |
| R-S2 | Bytes-per-flush framing: does each `Response.Flush()` produce one chunk, and are the hex sizes System.Web's own or IIS's re-framing (`HttpResponse.cs:681-682` says IIS7 re-frames) |
| R-S3 | `Response.End` after a flush: what goes on the wire — terminator only, or terminator plus something else |
| R-S4 | Client disconnects mid-stream: what the next `Response.Flush()` throws, what `IsClientConnected` reports, whether the pipeline continues |
| R-S5 | Exception thrown after a flush: confirm the status stays 200 and the salvage block + error text is appended (predicted by `:1543-1555`) |
| R-S6 | `BufferOutput = false`: one chunk per `Write`, or coalesced |
| R-WS1 | The 101 handshake byte for byte: which response headers survive from the ASP.NET pipeline (cookies set during `AcquireRequestState`? `X-AspNet-Version`?), and what IIS adds |
| R-WS2 | Subprotocol negotiation on the wire when `AspNetWebSocketOptions.SubProtocol` is set and when it is not |
| R-WS3 | What happens to response body bytes written after `AcceptWebSocketRequest` (e.g. by an `EndRequest` module) |
| R-WS4 | Close-frame handling: status/description propagation in both directions, and what the client sees when the callback throws |
| R-WS5 | Session availability in the callback, to confirm `Session == null` is the shipped 4.8 behavior and not a published-source artifact |
| R-WS6 | `AcceptWebSocketRequest` from `BeginRequest` and from `EndRequest`: exact exception types, to pin the ordering rule a classic pipeline must reproduce |

---

## 6. Ordered plan

**Streaming** (smaller, unblocks the compatibility row and is a prerequisite for
nothing else):

1. Take readings R-S1, R-S2, R-S3, R-S5 on the wire rig. Record them in this
   document.
2. Write the red raw-socket timing scenario (§5.1) plus the `RawSocketProbe`
   incremental reader. Confirm it fails for the timing reason and not another.
3. `ResponseSpool`: add `HasStatus`, the committed-head state, `CommitHeadAsync`,
   and a resumable `DrainPendingAsync`. Unit tests first.
4. `AspNetCoreWorkerRequest.FlushResponse`: commit-the-head-if-status-seen, then
   drain, sync-over-async through `BodyWriter`. Middleware `CommitAsync` becomes
   commit-if-needed + drain.
5. Run the full hosting suite; expect `ResponseEndOverKestrelTests`,
   `HeaderAmendmentOverKestrelTests`, `ResponseHeadersOverKestrelTests.H15` to
   stay green, and fix the P55 comment at `HttpResponse.cs:3249-3252`.
6. Add `SupportsAsyncFlush` + `BeginFlush`/`EndFlush` and an async-page streaming
   scenario.
7. Disconnect, exception-after-flush, `BufferOutput=false`, `TransmitFile`-after-
   flush, and h2 cases. Cross-platform round (Windows + Linux + macOS).
8. Amend ADR 0003 (the response model is no longer single-commit) and ADR 0006
   (write-side thread occupancy); flip the compatibility row; write the ledger row.

**WebSockets** (larger, and better started once streaming has settled the commit
state machine, because both touch the same middleware seam):

1. Stand up the IIS WebSocket rig and take R-WS1…R-WS6.
2. Add the three `HttpWorkerRequest` members and the two `HttpContext` edits;
   keep `HttpContextWebSocketsTests` green for `SimpleWorkerRequest`.
3. Implement the Kestrel `IWebSocketPipe` adapter with unit tests against a
   scripted `WebSocket` double.
4. Implement the middleware handoff and `UseWebSockets()` registration.
5. Build the `websocket` fixture + probe handler; the echo/close scenario.
6. Negotiation, error, ordering, and callback-context scenarios.
7. Cross-platform round; compatibility row; ledger row; close the follow-up.

---

## 7. Decisions needed — streaming

1. **Do we honor a mid-request flush at all, or keep buffering and document it?**
   *Recommendation:* honor it. Progress pages, server-sent events, and long
   downloads are ordinary Web Forms patterns, and the framing is already correct
   in the buffer — only the timing is wrong.
2. **How does a synchronous page's `Response.Flush()` perform an asynchronous
   Kestrel write?** *Recommendation:* block the pipeline thread on the write,
   writing through Kestrel's `BodyWriter` so the block is usually instant —
   the same trade already accepted for reading request bodies.
3. **Do we also implement the asynchronous flush contract
   (`SupportsAsyncFlush`)?** *Recommendation:* yes, as a second step — it costs
   little and means async pages never block a thread, but it cannot replace
   decision 2 because plain `Response.Flush()` never uses it.
4. **Do we turn on Kestrel's synchronous I/O switch instead?**
   *Recommendation:* no. It contradicts two ADRs and buys nothing over decision 2.
5. **Where does the "headers already sent" line live?**
   *Recommendation:* draw it exactly where System.Web already draws it — the
   first flush that generates a header block — and make the adapter refuse
   changes after that, rather than inventing a second rule.
6. **What happens when the commit fails after the first bytes have gone
   (unwritable body, file shrank, client vanished)?** *Recommendation:* abort the
   connection and log; a half-sent 200 cannot be turned into a 500, and pretending
   otherwise would send a corrupt response.
7. **Does `Response.End`'s deferred-header behavior (P55) change?**
   *Recommendation:* no. Guard the commit on "a status was sent", so `End`
   without a prior flush behaves exactly as today and `End` after a flush behaves
   as Framework's did.
8. **Do we take IIS readings first, or ship from source and read afterwards?**
   *Recommendation:* read first for the four wire questions (R-S1, R-S2, R-S3,
   R-S5); the rest is decidable from source.

## 8. Decisions needed — WebSockets

9. **Do we implement WebSockets on the classic pipeline at all, or keep pointing
   people at ASP.NET Core's own WebSocket middleware beside the port?**
   *Recommendation:* implement it. SignalR 1.x/2.x and hand-written handlers call
   `AcceptWebSocketRequest` directly, and there is no source-compatible substitute.
10. **Do we reuse Framework's `AspNetWebSocket` by writing a small adapter over
    Kestrel's socket, or hand the callback Kestrel's socket directly?**
    *Recommendation:* reuse `AspNetWebSocket`. The adapter is four methods, and
    it keeps the abort/close semantics and the connection counter applications
    can observe.
11. **Where does the upgrade happen — inside System.Web's pipeline, or in the
    middleware after the request finishes?** *Recommendation:* in the middleware,
    right after the pipeline completes. It avoids editing the pipeline, the step
    manager, and `HttpRuntime`, and it keeps the request alive naturally.
12. **Which imported files may change?** *Recommendation:* exactly three edits —
    add the capability members to `HttpWorkerRequest`, and let `HttpContext`'s two
    WebSocket entry points ask the worker request instead of casting to the IIS
    type. Everything else stays untouched.
13. **What does `IsWebSocketRequest` read?** *Recommendation:* Kestrel's own
    upgrade detection, and report "the module is not enabled" (Framework's own
    message) when the host has not registered WebSocket support.
14. **Does `UseRehostWebForms` register `UseWebSockets()` automatically?**
    *Recommendation:* yes, the way it already registers forwarded headers, so a
    consumer never has to know; leave the options overridable.
15. **Do we preserve the "cannot call from `BeginRequest`" ordering rule, which
    depends on an integrated-pipeline notification the classic pipeline does not
    track?** *Recommendation:* preserve it using the classic pipeline stage, and
    if that proves unreliable, drop it deliberately with a compatibility note
    rather than silently.
16. **Which response headers set by the application survive onto the 101?**
    *Recommendation:* decide from an IIS reading (R-WS1) rather than from taste;
    cookies set during the pipeline are the case that matters.
17. **What happens to body bytes written after the accept?**
    *Recommendation:* discard them — a 101 has no body — but confirm against IIS
    (R-WS3) before recording it as the contract.
18. **Do we support `AspNetWebSocketOptions.RequireSameOrigin` and subprotocol
    negotiation from day one?** *Recommendation:* yes; both are already implemented
    in imported, transport-neutral code and cost only tests.

## Framework readings

IIS 10 + .NET Framework 4.8.9344 on winbox, 2026-08-16 (IIS WebSocket Protocol
feature enabled), raw sockets through an ssh tunnel with per-read timestamps.
Pages: write/`Flush`/sleep 1.5 s/write/`Flush`/sleep 0.5 s/write; `End` after a
flush; `TransmitFile` between flushes; header/status/cookie change after a
flush; throw after a flush. Handler `Ws.ashx`: cookie + header before
`AcceptWebSocketRequest`, header (and, in one mode, a body write) after it, an
echo callback that first reports what it can see, `SubProtocol="chat"` and
`RequireSameOrigin` variants.

**R-S1 flush/delay/flush.** t=0.02 s: `HTTP/1.1 200 OK`, `Cache-Control:
private`, `Transfer-Encoding: chunked`, `Content-Type`, no `Content-Length`,
and the first chunk (`13\r\npart1-…\n\r\n`) in the same packet; t=1.53 s the
second chunk; t=2.05 s the third chunk plus `0\r\n\r\n`. Keep-alive: the
connection stays open after the terminator.
**R-S2 End after flush.** first chunk at t=0.02, then `6\r\npart2\n\r\n0\r\n\r\n`
at t=0.82; nothing after `End`.
**R-S3 TransmitFile between flushes.** the file and the write after it leave
as one chunk (`1a\r\nFILEDATA-0123456789\npart2\n\r\n`) at the next flush.
**R-S4 error after flush.** status stays 200; the error page arrives as one
chunk (`12a8\r\n<!DOCTYPE html>…`) then `0\r\n\r\n`.
**R-S5 head changes after flush.** `HttpException`: "Server cannot append
header after HTTP headers have been sent." / "Server cannot set status after
HTTP headers have been sent." / "Server cannot modify cookies after HTTP
headers have been sent."; `IsClientConnected` true.

**R-WS1 101.** `HTTP/1.1 101 Switching Protocols`, `Cache-Control: private`,
`Upgrade: websocket`, `Connection: Upgrade`, `Sec-WebSocket-Accept`, `Server`,
`X-AspNet-Version`, `X-Powered-By`, `Date`, **plus** the app's `Set-Cookie` and
the headers it appended before *and after* `AcceptWebSocketRequest`.
**R-WS2 body after Accept.** `Response.Write` after Accept puts the bytes on
the wire raw, right after the 101 and before the first frame (a client cannot
parse the stream); `Content-Type: text/html` appears only in that case.
**R-WS3 callback view.** `ctx.WebSocket` is `System.Web.WebSockets.AspNetWebSocket`;
`HttpContext.Current` set; `Response` throws `HttpException` "Response is not
available in this context."; `Session` null; `Items` kept (count 1); `User`
non-null; `Origin` echoes the request's `Origin` or null.
**R-WS4 subprotocol.** `Sec-WebSocket-Protocol: chat, superchat` +
`SubProtocol="chat"` → 101 with `Sec-WebSocket-Protocol: chat`, `SubProtocol`
"chat" in the callback; requested `other` → 500, `ArgumentException` "The
sub-protocol 'chat' cannot be negotiated for this request. See the
WebSocketRequestedProtocols property…".
**R-WS5 RequireSameOrigin.** no `Origin` or a foreign one → 403 "This type of
page is not served."; matching `Origin: http://127.0.0.1` → 101.
**R-WS6 close.** client close 1000 "client-bye" → server `CloseAsync` reply
1000 "bye:NormalClosure:client-bye", connection ends; server-initiated close →
1000 "server-close". `IsWebSocketRequest` on a plain GET: `False` (module
enabled); a plain GET to the handler: its own 400.
