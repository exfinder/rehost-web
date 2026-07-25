---
status: accepted
---

# Activate on the first routed request

By default, the first request routed to Rehost single-flights application
activation and waits outside System.Web while request-independent startup
completes. The same request then enters `HttpRuntime.ProcessRequest`, where
System.Web retains its distinct first-request initialization.

This mirrors classic IIS cold activation without exposing partial System.Web
state. Concurrent requests wait at the activation gate. Explicit eager preload
may be added later without changing the managed request contract.
