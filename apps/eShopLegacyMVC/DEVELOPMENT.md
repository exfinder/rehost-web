# eShop MVC development

[Running guide](README.md) · [Package mapping](../../docs/migration.md#package-mapping)

`.App` compiles the legacy WAP. `.Host` owns Kestrel and XDT.
`Autofac.Integration.Mvc` recompiles Autofac.Mvc5 4.0.2, whose Framework binary
binds Microsoft's System.Web and System.Web.Mvc. `eShopLegacy.Utilities` rebuilds
the solution's serialization library the Web API controllers reference.

## Dependencies

- Rehost.AspNet.Mvc, Rehost.AspNet.WebPages, Rehost.AspNet.WebApi.WebHost and
  Rehost.AspNet.Web.Optimization replace the Framework MVC, Web Pages, Web API
  host and bundling packages. Microsoft.AspNet.WebApi.Core 5.3.0 and Client 6.0.0
  (the versions the Rehost host requires) and Autofac.WebApi2 6.0.1 stay as shipped.
- Autofac.Integration.Mvc compiles against Autofac 4.0.1, the version its source
  declares; the application runs Autofac 6.1.0, as upstream does through its
  binding redirect. Its `ViewRegistrationSource` implements the Autofac 4
  `IRegistrationSource` signature; the application never registers it.
- EntityFramework 6.5.2 replaces the Framework-only 6.2.0; Newtonsoft.Json
  13.0.3 is the floor Rehost.AspNet.Web.Optimization sets.
- Application Insights, TelemetryCorrelation, the async session-state module,
  DotNetCompilerPlatform and Microsoft.Web.Infrastructure are not referenced.

## web.config

- XDT sets `UseMockData=true`; upstream ships `false` with a LocalDB connection.
  EF6 and the database initializer stay unreached; switching it off requires a
  portable connection and a real database journey.
- XDT removes the Session replacement and Application Insights/TelemetryCorrelation
  modules, whose binaries bind Framework. Built-in InProc session stays registered.
- CodeDOM settings and binding redirects are removed.
- `Views/Web.config` stays as upstream ships it; staging rewrites its
  `System.Web.Mvc` and `System.Web.WebPages.Razor` names.

## Open application scope

Create and delete posts, client-side validation, real EF operations,
customization data, `/api/files` (BinaryFormatter output) and log4net output remain
unassessed. The application supplies no log4net appender configuration. Runtime
support belongs to [compatibility](../../docs/dev/compatibility.md).

```text
eng/app-linux-smoke.sh eShopLegacyMVC 5088
```
