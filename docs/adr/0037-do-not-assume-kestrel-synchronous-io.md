---
status: accepted
---

# Do not assume Kestrel synchronous I/O

Keep Kestrel `AllowSynchronousIO` disabled by default. The later request-body
slice must probe Framework body-read behavior and design an asynchronous
Kestrel producer that serves System.Web synchronous and asynchronous
`HttpWorkerRequest` reads.

A legacy synchronous read may still occupy its managed pipeline thread.
Enabling Kestrel synchronous stream I/O is an explicit evidence-backed fallback,
not the default architecture. Eagerly buffering every request body before
System.Web is not assumed to be compatible.

## Thread-pool coupling

The occupied thread and the continuation that releases it come from the same
pool. `RehostWebFormsMiddleware` runs `ProcessRequest` inline on the ASP.NET Core
request thread, a pooled worker, which a synchronous `ReadEntityBody` then blocks
awaiting `PipeReader.ReadAsync` — whose completion Kestrel schedules back onto
that pool. IIS did not have this shape: the blocked managed thread and the native
I/O completion that released it were separate pools.

The consequence is a latency cliff, not a hang. The pool's maximum is far above
any reachable concurrency and its starvation heuristic keeps injecting threads at
roughly one per second, so it always recovers; during the ramp every request is
affected, including body-less ones that can never block.

Two properties bound it. A read whose bytes are already buffered completes
synchronously and never yields, which is the ordinary postback. Kestrel's
`MinRequestBodyDataRate` — 240 bytes/second after a five-second grace, on by
default — aborts a slow client, which surfaces as a terminal state and releases
the thread. What remains is bodies fast enough to clear that rate yet slow enough
to block, at a concurrency high enough to exhaust the pool.

The application-level opt-out is Framework's own: `httpRuntime asyncPreloadMode`
drives `ImplicitAsyncPreloadModule` to read the entity asynchronously to EOF
before the handler, after which synchronous reads are served from buffered bytes
and never block. It stays opt-in because it buffers the whole body and changes
streaming behavior for bufferless readers.

Relocating the blocking to a non-pool thread was considered and rejected: it
costs a dedicated OS thread per body-carrying request with no reuse, and it
depends on thread-affinity assumptions — `ExecutionContext` flow, the lock
identity recorded as P38's neighbour P33, stack size — that no test covers. The
cliff is measured rather than pre-empted; see
[request-body concurrency](../follow-ups/request-body-concurrency.md).
