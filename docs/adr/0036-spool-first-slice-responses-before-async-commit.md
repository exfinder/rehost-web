---
status: accepted
---

# Spool first-slice responses before asynchronous commit

The first-slice worker request records status, headers, memory body fragments,
and logical flush state in a per-request spool. Small responses remain in
memory; larger responses spill beneath the application work root.

`EndOfRequest` seals managed output and completes the application-runtime wait.
The Kestrel adapter then commits the sealed response asynchronously and disposes
the spool. A disconnect is observable by System.Web but does not abandon
managed completion. A later commit failure is an adapter failure.

The worker request does not enable Kestrel synchronous I/O. Intermediate
client-visible streaming is outside the first-slice transport envelope.
