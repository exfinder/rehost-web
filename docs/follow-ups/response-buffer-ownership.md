# Response buffer ownership and zero-copy send

Status: open. Priority: low until a slice measures the copy. Ledger row: P28.

## Background

.NET Framework accumulated response bytes in 31KB buffers rented from a native
pool in `webengine4.dll`, and recycled them on every flush — `ClearBuffers`
calls `RecycleBufferElements`, carrying the original note that private bytes
went "thru roof" when flush did not recycle.

Recycling a pooled buffer while the transport still holds it is safe only
because ownership transferred explicitly. The unmanaged element sent through
`SendResponseFromMemory(IntPtr, int, bool isBufferFromUnmanagedPool)`, and
`ISAPIWorkerRequest` wrapped the pointer in `MemoryBytes` with
`BufferType.UnmanagedPool`. Both sides then held a reference count, so the
buffer returned to the pool only once the send had consumed it.

The `byte[]` overload carries no such flag, and its implementations retain what
they receive: `ISAPIWorkerRequest.SendResponseFromMemory(byte[], int)` calls
`AddBodyToCachedResponse(new MemoryBytes(data, length))` without copying. That
is why Framework's managed `HttpResponseBufferElement` sets `_recycle = false`
and never returns anything anywhere.

## Current position

`HttpResponseManagedBufferElement` rents from `ArrayPool<byte>` for
accumulation and surrenders an exact-size copy from both `Send` and `GetBytes`.
The rented array never escapes the element, so no worker request, adapter, or
transport carries any lifetime obligation, and aliasing is impossible rather
than merely documented.

This allocates exactly the response size per request instead of rounding up to
31KB granularity, so it produces strictly less garbage than not pooling. The
advantage is largest on small responses and converges to zero on large ones,
where the copy itself becomes the dominant cost.

## What a zero-copy design would require

Framework's reference count lived in the pool keyed by the buffer, not in the
unmanaged memory, so a managed pool can hold the same counter. The unsolved
part is the handoff: `SendResponseFromMemory(byte[], int)` gives the callee no
object to reference. It needs either an overload carrying a lease, or a side
table keyed by array instance.

Neither removes the obligation — it relocates it. A transport that omits the
retain corrupts responses across requests, a double release corrupts, and an
omitted release leaks pool buffers permanently. All three fail silently and
only under concurrency.

## Decide when, not now

Revisit when a slice both (a) measures the copy as material, and (b) has a real
transport to validate the protocol against. Client-visible streaming and file
send — where zero-copy actually earns its keep — are outside the first-slice
transport envelope by
[ADR 0035](../adr/0035-limit-the-first-slice-transport-envelope.md).

## Done when

Either the copy is measured immaterial and this is closed, or a refcounted
handoff exists with a transport that exercises retain, release, double-release,
and abandoned-request paths under concurrency.
