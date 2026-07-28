# First runnable request

Status: both gate columns pass on macOS `arm64` and Windows `x64`. Canonical
scope:
[managed runtime port plan, slice 1](../core-runtime-port-plan.md#vertical-slices).

## Goal

Serve a bodyless request through Kestrel, public
`HttpRuntime.ProcessRequest(HttpWorkerRequest)`, the complete classic managed
pipeline, one configured module, one configuration-mapped precompiled handler,
and normal System.Web completion on every supported platform.

The handler assembly exists only in fixture `<application>/bin`; the Kestrel
host does not reference it.

## Included

- first-request activation through `ApplicationManager`;
- retained `HostingEnvironment`, `HttpRuntime`, `BuildManager`, and
  `HttpApplicationFactory` sequencing;
- configured `system.web/httpModules` and `system.web/httpHandlers`;
- pooled `HttpApplication`, sync and async handlers, `CompleteRequest`, errors;
- bodyless Kestrel worker request and asynchronously committed response spool;
- initialization-error response and terminal shutdown notification;
- .NET Framework differential traces.

## Deferred

- dynamic `Global.asax`, `App_Code`, `.ashx`, and `.aspx` compilation;
- request bodies, path info, streaming response, and file send;
- session, authentication, cache, routing, and resources;
- graceful drain/disposal.

## Work packets

- [.NET Framework differential harness](framework-differential-harness.md)
- [ASP.NET Core host adapter](aspnet-core-host-adapter.md)
- [Request startup portability](request-startup-portability.md)
- [Pipeline fixture profile](minimal-pipeline-feature-profile.md)
- [Completion, failure, and cancellation](request-completion-failure-and-cancellation.md)
- [Managed response output](managed-response-buffering-and-output.md)
- [Portable request diagnostics](portable-request-diagnostics.md)
- [Windows platform diagnostics](windows-platform-diagnostics.md)
- [Compatibility feature map](compatibility-feature-map.md)

## Done when

Every scenario in
[the first-slice parity gate](../adr/0031-require-the-first-slice-parity-gate.md)
passes, the fixture handler resolves without a host reference, and the
supported path reaches no native IIS/Windows operation.

All three hold on macOS `arm64` and Windows `x64`, across both the
[differential](../../prototypes/portable-parity/README.md) and
[adapter](../../prototypes/adapter-parity/README.md) columns.

What the slice does not cover is recorded under "Deferred" above and in
[the adapter's open list](aspnet-core-host-adapter.md). Notably the response
spill path and mid-request disconnect are implemented but unexercised, because no
declared scenario reaches either.
