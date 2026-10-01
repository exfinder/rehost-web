# Response buffer ownership and zero-copy send

Ledger P28 records the current safe contract:
`HttpResponseManagedBufferElement` accumulates in a rented array, then hands
the worker request an exact-size copy. The rented array never escapes, so no
adapter or transport owns its lifetime. Streaming, long file send, and
WebSockets are independently supported by ledger P78-P80.

Framework avoided this copy through native pooled buffers with explicit retain
and release. The managed `byte[]` worker-request API carries no lease, so a
portable zero-copy design needs either a lease-bearing overload or an
array-identity side table. Missing retain, double release, or abandoned-request
release corrupts data or leaks silently under concurrency.

## Open

Revisit only when measurements show the copy is material and a real transport
can exercise ownership transfer.

## Done when

Either measurements close the item, or a refcounted handoff proves retain,
release, double-release, failure, and abandonment under concurrency.
