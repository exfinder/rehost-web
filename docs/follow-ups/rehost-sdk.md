# Rehost.WebForms.Sdk MSBuild SDK package

Today a ported application is two SDK projects whose consumer contract rides in
package `build/`/`buildTransitive/` targets: the app library sets
`RehostAppContentRoot` (WAP-faithful compile of the legacy folder, from the
runtime package's targets) and the host exe sets `RehostSiteContentRoot`
(staging, XDT config pipeline, run/publish layout, from the hosting package's
targets).

The next step in friction removal is an MSBuild SDK package:

```xml
<Project Sdk="Rehost.WebForms.Sdk">
  <PropertyGroup>
    <RehostSiteContentRoot>../MyApp/</RehostSiteContentRoot>
    <OutDir>rehost_root/bin/</OutDir>
  </PropertyGroup>
</Project>
```

An SDK can own both project shapes (`Rehost.WebForms.Sdk` /
`Rehost.WebForms.Sdk.Host`), inject the `Rehost.WebForms` reference, and replace the
remaining boilerplate (TFM, AssemblyName, GenerateAssemblyInfo) with defaults.
It is also the natural home for a `Microsoft.WebApplication.targets`
replacement if the Web Site project model lands
([web-site-vs-wap-project-models](web-site-vs-wap-project-models.md)).

## Prototype with the split layout (2026-09-20, macOS)

Two SDK packages, `Rehost.WebForms.Sdk` for the App over `Microsoft.NET.Sdk` and
`Rehost.WebForms.Sdk.Host` over `Microsoft.NET.Sdk.Web`, built the stock
template in the [split layout](migration-stages-and-site-layout.md) and passed
its smoke, first through explicit imports and then packed and consumed as
`<Project Sdk="Rehost.WebForms.Sdk/0.0.1-proto">`. Each csproj shrank to its
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

Open: how the Host finds `rehost_root/` in development and after publish
without probing, one SDK package or two, injecting the `Rehost.WebForms`
package references, publish wiring, and Windows and Linux. The NuGet SDK
resolver reads `NuGet.config` only, so the apps' local feed needs a config
entry beside `RestoreAdditionalProjectSources`.

Costs that kept it out of M2: SDK resolution and version pinning move to
`global.json` (a second version surface beside PackageReference), defaults
ordering is subtler than package targets (SDK props run before the project
body), and the current two-package contract had not yet proven its property
names stable. Revisit once a second real application has been ported and the
knob set has settled.
