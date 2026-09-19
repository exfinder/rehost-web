# Rehost.WebForms.Sdk.App and .Host MSBuild SDK packages

Today a ported application is two SDK projects whose consumer contract rides in
package `build/`/`buildTransitive/` targets: the app library sets
`RehostAppContentRoot` (WAP-faithful compile of the legacy folder, from the
runtime package's targets) and the host exe sets `RehostSiteContentRoot`
(staging, XDT config pipeline, run/publish layout, from the hosting package's
targets).

The next step in friction removal is an MSBuild SDK package:

```xml
<Project Sdk="Rehost.WebForms.Sdk.Host/1.0.0">
  <ItemGroup>
    <ProjectReference Include="../MyApp.App/MyApp.App.csproj" />
  </ItemGroup>
</Project>
```

Two SDKs own the two project shapes (`Rehost.WebForms.Sdk.App` /
`Rehost.WebForms.Sdk.Host`, named after the `.App` and `.Host` projects), inject the `Rehost.WebForms` reference, and replace the
remaining boilerplate (TFM, AssemblyName, GenerateAssemblyInfo) with defaults.
It is also the natural home for a `Microsoft.WebApplication.targets`
replacement if the Web Site project model lands
([web-site-vs-wap-project-models](web-site-vs-wap-project-models.md)).

## Prototype with the split layout (2026-09-19, macOS)

Two SDK packages, one for the App over `Microsoft.NET.Sdk` and one for the
Host over `Microsoft.NET.Sdk.Web`, built the stock
template in the [split layout](migration-stages-and-site-layout.md) and passed
its smoke, first through explicit imports and then packed and consumed as
`<Project Sdk="<id>/0.0.1-proto">`. Each csproj shrank to its
package and project references.

- An SDK's targets run before the .NET SDK derives the output paths, so it can
  set `OutDir`, which a package's targets cannot. The App SDK sends the build
  to `<Host>/rehost_root/bin/`; the csproj carries no `OutDir` and no check.
- The App SDK sets the legacy compile defaults, `EnableDynamicLoading`, the
  assembly name without `.App`, and `PrivateAssets="all"` on every project
  reference. The Host SDK marks the `*.App` reference `Private="false"
  ExcludeAssets="runtime;native"`, defaults `RehostSiteContentRoot`, and deletes
  from `bin/` what the host folder already holds (19 files left).
- A csproj value wins in both places: defaults in `Sdk.props` are overwritten
  by the project body, defaults in `Sdk.targets` are conditional. Verified for
  `GenerateAssemblyInfo`, `Nullable` and `OutDir`.
- Item metadata cannot be read in the condition of an evaluation-time
  `Update`; the App reference is matched by pattern (`../*/*.App.csproj`).
- A second build with no change took two seconds and left `bin/` as it was.

## Two packages, not one (decided 2026-09-19)

A property in the csproj cannot choose between the App and the Host shape: the
base SDK is imported by `Sdk.props`, before the project body is read. One
add-on package does work when it is listed ahead of the base SDK
(`Sdk="Rehost.WebForms.Sdk/x;Microsoft.NET.Sdk.Web"`): its targets still run
before the output paths are derived, the base SDK names the kind, defaults
(including `TargetFramework`) and csproj overrides behave, and the stock
template passed its smoke. It was not chosen. The line is long and
order-sensitive, a reversed order sets `OutDir` too late, and a missing base SDK
builds nothing while reporting success; both need a check of ours to be
visible. Two ids give one short name per project kind and no order rule, which
is the smaller thing for a migrator to hold. Leaving is one edit either way:
at the last migration stage the Host's `Sdk` becomes `Microsoft.NET.Sdk.Web`.

Open: how the Host finds `rehost_root/` in development and after publish
without probing, injecting the `Rehost.WebForms` package references, publish
wiring, and Windows and Linux. The NuGet SDK
resolver reads `NuGet.config` only, so the apps' local feed needs a config
entry beside `RestoreAdditionalProjectSources`.

Costs that kept it out of M2: SDK resolution and version pinning move to
`global.json` (a second version surface beside PackageReference), defaults
ordering is subtler than package targets (SDK props run before the project
body), and the current two-package contract had not yet proven its property
names stable. Revisit once a second real application has been ported and the
knob set has settled.
