---
status: accepted
---

# Keep application activation host-neutral

The application runtime owns activation, state publication, request admission,
System.Web dispatch, and shutdown policy. The ASP.NET Core adapter selects
requests, translates Kestrel state into `HttpWorkerRequest`, awaits completion,
and relays lifecycle signals.

This retains the classic hosting shape: the host boundary triggers activation
outside `HttpRuntime`, while the application runtime delegates legacy
initialization sequencing to retained `ApplicationManager` and
`HostingEnvironment` machinery.
