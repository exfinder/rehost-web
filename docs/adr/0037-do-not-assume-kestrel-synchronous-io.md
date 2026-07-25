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
