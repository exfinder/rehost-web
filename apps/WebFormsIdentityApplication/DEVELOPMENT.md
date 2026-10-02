# Web Forms Identity development

[Running guide](README.md) · [Package mapping](../../docs/migration.md#package-mapping)

`.App` preserves `WebFormsIdentityApplication.dll`; `.Host` owns Kestrel,
configuration transforms and SQLite initialization. The legacy WAP remains a
separate input tree. Shared repository workflow is in the
[plain template notes](../WebFormsApplication/DEVELOPMENT.md).

## Dependency choices

Identity and OWIN middleware use their shipped managed packages; the System.Web
OWIN host uses its Rehost counterpart. Katana 4.x directly registers its module,
so the original Microsoft.Web.Infrastructure reference is unnecessary. EF6 uses
6.5.2 for the maintained SQLite provider. Newtonsoft.Json has an explicit serviced
pin because the middleware requests an older transitive version.

## SQLite initialization

The host replaces LocalDb with `Data Source=|DataDirectory|Identity.db;Foreign
Keys=True`. SQLite is this application's fixture database; it does not establish
SQL-provider support for the runtime.

- XDT registers `SQLiteFactory` in `system.data/DbProviderFactories`; EF's provider
  services alone cannot identify the connection's factory.
- The SQLite EF6 provider generates no DDL. The host applies `Identity.schema.sql`
  to a missing database and disables EF database initialization afterwards.
- Apply raw DDL before first request; constructing an EF context then initializes
  configuration before the runtime installs its mapped configuration system.
- The 2.0.x provider uses `SQLitePCLRaw.lib.e_sqlite3`, including arm64 assets.
  The older interop provider lacks the required native arm64 closure.

## Configuration

The Host XDT replaces package defaults, removes runtime/CodeDOM settings, retargets
Optimization controls and installs the SQLite connection, factory and provider.
The merged `modules` collection honors `remove name="FormsAuthentication"`.
An unresolvable session provider is not constructed under `mode="InProc"`.
Auto-generated machine keys persist per application; scale-out requires shared
explicit keys. See [machine keys](../../docs/migration.md#machine-keys).

```text
eng/app-linux-smoke.sh WebFormsIdentityApplication 5082
```

## Open application scope

External login providers, confirmation/reset mail, SMS and two-factor flows
remain unassessed; the template does not enable them.
