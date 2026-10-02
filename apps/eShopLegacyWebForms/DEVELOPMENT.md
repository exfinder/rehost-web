# eShop development

[Running guide](README.md) · [Package mapping](../../docs/migration.md#package-mapping)

`.App` compiles the legacy WAP and supplies jquery/bootstrap script mappings.
`.Host` owns Kestrel and XDT. `Autofac.Integration.Web` recompiles Autofac.Web
4.0.0, whose Framework binary binds Microsoft's System.Web.

## web.config

- `UseMockData=true` leaves EF6 and LocalDb unreached; switching it off requires
  a portable connection and a real database journey.
- XDT removes the Session replacement and Application Insights/TelemetryCorrelation
  modules, whose binaries bind Framework. Built-in InProc session stays registered.
- The Autofac container-disposal and property-injection modules remain configured
  against the recompiled integration assembly.
- App-local `PreApplicationStartCode` registers jquery/bootstrap against committed
  physical files. General Rehost ScriptManager packages register their own names.
- CodeDOM settings are removed; Optimization controls use the Rehost assembly.
  `debug`, `requestValidationMode`, InProc session and the unused EF default
  connection factory remain application-owned.
- RouteUrl expressions use the runtime baseline's expression-builder registration.

## Open application scope

Catalog mutation postbacks and validators, real EF operations, customization data,
mobile views and log4net output remain unassessed. The application supplies no
log4net appender configuration. Runtime support belongs to
[compatibility](../../docs/dev/compatibility.md).

```text
eng/app-linux-smoke.sh eShopLegacyWebForms 5083
```
