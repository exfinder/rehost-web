---
status: accepted
---

# Use one deep host-neutral application interface

Host integration creates one process-scoped `WebFormsApplication` from explicit
options, submits `HttpWorkerRequest` instances asynchronously, and eventually
stops the application. The first submitted request triggers activation.

`ApplicationManager`, `HostingEnvironment`, activation sequencing, static-state
publication, and completion bridging remain internal. ASP.NET Core integration
depends only on this application interface; exact member names remain
provisional until implementation.
