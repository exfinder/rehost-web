---
status: accepted
---

# Use the classic managed pipeline for the initial runtime

The initial portable runtime uses the classic managed request pipeline because
the host-neutral `HttpWorkerRequest` seam lets System.Web own the complete
request lifecycle. Recreating IIS-integrated notification scheduling would add
a second hosting engine before the portable managed runtime is proven.

IIS-integrated notifications, native modules, w3wp/AppDomain management, and
recycle behavior are outside this initial contract.
