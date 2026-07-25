---
status: accepted
---

# Do not preflight System.Web configuration

The separate mapped-configuration preflight introduced by `fcc0372` is not
part of Framework initialization and must be removed from the target design.

Before legacy global mutation, validate only the host-owned contract: identity,
roots, baseline assets, writable work storage, and ownership conflicts. Do not
call `OpenMappedWebConfiguration` or eagerly resolve application sections.

Retained `HostingEnvironment` and `HttpRuntime.HostingInit` sequencing installs,
reads, completes, and reports System.Web configuration. Portable policy checks
belong at the existing section-consumption leaves so configuration caching,
exception timing, and System.Web error responses remain Framework-shaped.
