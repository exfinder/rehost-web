# First runnable request

Status: open. Priority: highest. Canonical scope:
[managed runtime port plan, slice 1](../core-runtime-port-plan.md#slice-1-bodyless-precompiled-handler).

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

## Done when

Every scenario in
[the first-slice parity gate](../adr/0031-require-the-first-slice-parity-gate.md)
passes, the fixture handler resolves without a host reference, and the
supported path reaches no native IIS/Windows operation.
