# App and Host MSBuild SDK packages

Priority lives in [backlog](../backlog.md). Package targets already own the
App/Host contract; `dotnet new rehost-web` writes its project boilerplate.
An SDK is useful only if Web Site support or a different layout needs defaults
that package targets cannot supply.

## Proposed contract

- Separate `Rehost.Web.Sdk.App` and `.Host` IDs select the base SDK before the
  project body is evaluated. A single add-on SDK would require an order-sensitive
  SDK list and checks for a missing or reversed base SDK.
- Defaults cover TFM, assembly identity, compiler switches, package references,
  content roots and `OutDir`; explicit project values win.
- Keep one NuGet graph per process and preserve the package-target contract.
- Switching the Host back to `Microsoft.NET.Sdk.Web` must remain sufficient at
  the end of migration.

## Open decisions

SDK resolution happens before targets can pack the repository feed and reads
`NuGet.config`, not `RestoreAdditionalProjectSources`. A clean clone needs either
an earlier pack step or explicit source imports of SDK props/targets. The latter
preserves the one-command repository build without environment-wide SDK overrides.

Decide package-reference injection, publish behavior and Windows/Linux consumption
if the SDK becomes necessary. Revisit for a Web Site targets replacement or a
split-layout requirement, not for the existing few lines of project boilerplate.
