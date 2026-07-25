---
status: accepted
---

# Use a managed .NET Framework semantic oracle

Portable pipeline behavior is established with controlled applications running
on .NET Framework 4.8.1 through `HttpRuntime.ProcessRequest(HttpWorkerRequest)`.
Pinned Reference Source explains the observed behavior but does not replace
executable evidence. IIS-hosted probes are reserved for behavior owned by IIS
that cannot be isolated through the managed entry.
