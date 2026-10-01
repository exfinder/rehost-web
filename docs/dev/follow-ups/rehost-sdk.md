# Rehost.Web.Sdk.App and .Host MSBuild SDK packages

Today a ported application is two SDK projects whose consumer contract rides in
package `build/`/`buildTransitive/` targets: the app library sets
`RehostAppContentRoot` (WAP-faithful compile of the legacy folder, from the
runtime package's targets) and the host exe sets `RehostSiteContentRoot`
(staging, XDT config pipeline, run/publish layout, from the hosting package's
targets).

The next step in friction removal is an MSBuild SDK package:

```xml
<Project Sdk="Rehost.Web.Sdk.Host/1.0.0">
  <ItemGroup>
    <ProjectReference Include="../MyApp.App/MyApp.App.csproj" />
  </ItemGroup>
</Project>
```

Two SDKs own the two project shapes (`Rehost.Web.Sdk.App` /
`Rehost.Web.Sdk.Host`, named after the `.App` and `.Host` projects), inject the `Rehost.Web` reference, and replace the
remaining boilerplate (TFM, AssemblyName, GenerateAssemblyInfo) with defaults.
It is also the natural home for a `Microsoft.WebApplication.targets`
replacement if the Web Site project model lands
([web-site-vs-wap-project-models](web-site-vs-wap-project-models.md)).

## Parked (2026-09-20)

Not built. With the split layout [not adopted](migration-stages-and-site-layout.md),
the SDK no longer carries layout wiring. What is left is about seven lines per
csproj: `TargetFramework`, `OutputType`, `ImplicitUsings`, `Nullable`,
`GenerateAssemblyInfo`, the assembly name, the two Rehost package references, and
`OutDir`, the one line a package cannot take. The consumer contract stays in the
package targets either way.

A migrator's csproj is already the one under `apps/`. The `Directory.Build.*`,
`LocalFeed.props` and `eng/LocalFeed.targets` files there only pack `src/` into a
local feed; `eng/external-consumer.sh` builds the stock pair without them, from a
feed and nuget.org. The only edit is a literal package version. Writing those
lines once is the job of the `Rehost.Web.Templates` package
(`dotnet new rehost-web`, [getting started](../../getting-started.md)), which
landed on 2026-09-21.

Costs for seven lines: two more packages to ship and version, one more thing to
remove at the last migration stage, and no way for this repo to consume its own
SDK by id on a clean clone (below).

Revisit if the Web Site project model lands and needs a
`Microsoft.WebApplication.targets` replacement, or if the split layout returns.

### An SDK cannot be resolved from a local path

Measured on SDK 10.0.302, macOS. `<Project Sdk="…">` takes a name and a version,
never a path or a property, and MSBuild resolves it while it evaluates the first
line, before any target of ours can pack a feed. The NuGet SDK resolver reads
`NuGet.config` only, not `RestoreAdditionalProjectSources`.

- `MSBuildSDKsPath=<folder>` alone: the SDK is not found.
- `DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=<folder>` alone: not found.
- Both: the local SDK resolves and `Microsoft.NET.Sdk` no longer does. The
  variables replace the SDK folder instead of adding to it, and a repo file
  cannot set them.

In this repo the choices are a pack step before the first build, or importing
`Sdk/Sdk.props` and `Sdk/Sdk.targets` by path from `src/` (one command, two
`Import` lines per csproj, agreed as the route if the SDK is built). A migrator
on nuget.org has neither problem.

## Prototype (2026-09-19, macOS)

Two SDK packages, one for the App over `Microsoft.NET.Sdk` and one for the
Host over `Microsoft.NET.Sdk.Web`, built the stock template and passed its
smoke, first through explicit imports and then packed and consumed as
`<Project Sdk="<id>/0.0.1-proto">`. Each csproj shrank to its package and
project references. The prototype ran on a split layout that was
[not adopted](migration-stages-and-site-layout.md); what it showed about SDKs
holds for the current one.

- An SDK's targets run before the .NET SDK derives the output paths, so it can
  set `OutDir`, which a package's targets cannot. The Host SDK can send the
  build to `rehost_root/bin/`; the csproj then carries no `OutDir` line and
  `RehostVerifyOutDir` is needed only by the long form.
- The App SDK sets the legacy compile defaults and the assembly name without
  `.App`. The Host SDK defaults `RehostSiteContentRoot`. The `-o` redirect
  stays, inside the hosting targets.
- A csproj value wins in both places: defaults in `Sdk.props` are overwritten
  by the project body, defaults in `Sdk.targets` are conditional. Verified for
  `GenerateAssemblyInfo`, `Nullable` and `OutDir`.
- Item metadata cannot be read in the condition of an evaluation-time
  `Update`; a reference is matched by pattern (`../*/*.App.csproj`).
- A second build with no change took two seconds and left `bin/` as it was.

## Two packages, not one (decided 2026-09-19)

A property in the csproj cannot choose between the App and the Host shape: the
base SDK is imported by `Sdk.props`, before the project body is read. One
add-on package does work when it is listed ahead of the base SDK
(`Sdk="Rehost.Web.Sdk/x;Microsoft.NET.Sdk.Web"`): its targets still run
before the output paths are derived, the base SDK names the kind, defaults
(including `TargetFramework`) and csproj overrides behave, and the stock
template passed its smoke. It was not chosen. The line is long and
order-sensitive, a reversed order sets `OutDir` too late, and a missing base SDK
builds nothing while reporting success; both need a check of ours to be
visible. Two ids give one short name per project kind and no order rule, which
is the smaller thing for a migrator to hold. Leaving is one edit either way:
at the last migration stage the Host's `Sdk` becomes `Microsoft.NET.Sdk.Web`.

Open: injecting the `Rehost.Web` package references, publish, and
Windows and Linux. The NuGet SDK resolver reads `NuGet.config` only and runs at
evaluation, so the apps' local feed needs a config entry beside
`RestoreAdditionalProjectSources`, and a clean clone must still build with one
command before the feed is packed.

Costs that kept it out of M2: SDK resolution and version pinning move to
`global.json` (a second version surface beside PackageReference), defaults
ordering is subtler than package targets (SDK props run before the project
body), and the current two-package contract had not yet proven its property
names stable. Revisit once a second real application has been ported and the
knob set has settled.
