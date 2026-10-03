# Framework assembly facades

Staging rewrites Framework assembly names in configuration
([migration reference](../migration-reference.md#configuration)). Compiled
libraries carry the same names in their metadata, and nothing rewrites those:
a binary built against `System.Web.Mvc` or `System.Web` does not bind until it
is recompiled against the Rehost assemblies. Type-forwarding facades under the
original names would remove that step.

## Measured binder behavior

Measured 2026-10-04 on SDK 10.0.302 with a scratch probe (not committed):

- An unsigned type-forwarding facade named `System.Web.Mvc`, version 5.3.0.0,
  in the TPA satisfies
  `Type.GetType("…, System.Web.Mvc, Version=5.2.3.0, Culture=neutral, PublicKeyToken=31BF3856AD364E35")`
  and `Assembly.Load` of the same name.
- The binder ignores the public key token and requires the facade's version to
  be at least the requested one: a 6.0.0.0 request returns null.
- Public-signing the facade with Microsoft's key changes only the reported
  name.
- `AssemblyLoadContext.Resolving` cannot alias names; the runtime rejects the
  result with "Resolved assembly's simple name must be the same as of the
  requested assembly". A `Resolving` handler that loads a facade with
  `LoadFromAssemblyPath` bypasses the version and token checks entirely.

## Options

1. Facades for the AspNetWebStack family (`System.Web.Mvc`,
   `System.Web.WebPages*`, `System.Web.Http.WebHost`,
   `Microsoft.Web.Infrastructure`), shipped inside the Rehost packages. The
   staging rewrite is no longer needed for those names, and Framework binaries
   such as Autofac.Mvc5 bind unmodified.
2. A `System.Web.dll` facade over Rehost.Web, Extensions, Services,
   ApplicationServices, Abstractions and Routing. Framework-compiled
   `System.Web` libraries (Elmah, Application Insights, Autofac.Web, the
   shipped Microsoft.AspNet.Mvc, WebPages and WebHost, Katana) load without
   recompiling, and the recompiled companions become optional.

Forwarder lists are generated from the built assemblies in a build step, in
the GenFacades style.

## Costs

- Failures move from build or activation to first call
  (`MissingMethodException`, `TypeLoadException`) unless a load-time member
  check is added.
- Shipped binaries keep their Windows-only paths, which the recompiles patch
  today: `SHA256Cng` in Web Pages AntiForgery and Crypto and in MVC
  OutputCache child actions, AppDomain and registry use in
  WebPages.Deployment.
- The roadmap's scope statement that libraries bound to Framework
  `System.Web` require recompilation changes.
