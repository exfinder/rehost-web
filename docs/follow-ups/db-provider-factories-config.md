# Provider factory configuration

## Problem

An application cannot name its ADO.NET provider in configuration. On Framework,
`machine.config` declares `<section name="system.data"
type="System.Data.Common.DbProviderFactoriesConfigurationHandler, System.Data">`
and carries a `<system.data><DbProviderFactories>` list; `DbProviderFactories`
materializes those rows and activates a factory's `Instance` member on demand.
Modern `System.Data.Common` dropped the handler — the assembly has no
configuration-reading type at all — and `DbProviderFactories` is an in-memory
registry fed only by `RegisterFactory`.

Consequences today:

- The shipped baseline `machine.config` declares no `system.data` section, so a
  migrated `web.config` carrying one is a section the configuration system does
  not recognize (unverified: the failure mode was not measured).
- Registering a provider is host code. `WebFormsIdentityApplication` calls
  `DbProviderFactories.RegisterFactory` for SQLite in its host.
- The gap reaches declarative markup, not only host wiring: imported
  `SqlDataSource` resolves `ProviderName` through
  `DbProviderFactories.GetFactory` (`SqlDataSource.cs:933`), so
  `<asp:SqlDataSource ProviderName="System.Data.SQLite">` has no way to work.

## Candidate

Declare the section with a runtime-owned type and bridge it: during application
initialization, read `<system.data><DbProviderFactories>` and call
`DbProviderFactories.RegisterFactory(invariant, assemblyQualifiedTypeName)`. The
string overload defers type loading, which preserves Framework's late failure —
a stale row does not take the application down until something asks for it.
`GetFactoryClasses` and `GetFactory(DataRow)` still exist, so the `DataTable`
surface follows from registration.

Open decisions:

- Which rows the baseline `machine.config` ships. Framework's are Odbc, OleDb,
  OracleClient, and SqlClient; the first three are out of contract, and SqlClient
  should register only when its assembly is present.
- `<clear/>` and `<remove invariant="..."/>` semantics against machine-level
  rows.
- Factory type resolution through application `bin` probing, since the provider
  assembly lives with the application, not the host.
- The registry is process-global with no unload path, which one application per
  process makes tolerable but should be stated.

## Done when

- A configured provider resolves without host code, on all three platforms.
- `SqlDataSource` with a non-SqlClient `ProviderName` is covered by a test.
- The recognized-section behavior for a migrated `<system.data>` is decided:
  honored, or refused with an actionable diagnostic.
