---
status: accepted
---

# Enter through HttpRuntime.ProcessRequest

Host adapters submit each `HttpWorkerRequest` through the public classic entry,
`HttpRuntime.ProcessRequest`. This preserves the integrated-mode guard, initial
request accounting, request-queue participation, and normal dispatch path.

Adapters must not call `ProcessRequestNow`; that internal shortcut bypasses
classic admission and queue behavior.
