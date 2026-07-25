---
status: accepted
---

# Activate through a registered lifecycle bridge

Application activation calls `ApplicationManager.CreateObjectInternal` with an
internal portable `IRegisteredObject`. This retains
`ApplicationManager.GetAppDomainWithHostingEnvironment`, its locking and
bookkeeping, `HostingEnvironment` initialization, object registration, and
`Stop` callbacks.

The registered object only bridges lifecycle notification to the
process-scoped application owner. It does not process requests. Requests
continue to enter through `HttpRuntime.ProcessRequest(HttpWorkerRequest)`, and
the host never calls `HostingEnvironment.Initialize` directly.
