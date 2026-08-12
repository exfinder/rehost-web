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
  </PropertyGroup>
</Project>
```

An SDK can own both project shapes (`Rehost.WebForms.Sdk` /
`Rehost.WebForms.Sdk.Host`), inject the metapackage reference, and replace the
remaining boilerplate (TFM, AssemblyName, GenerateAssemblyInfo) with defaults.
It is also the natural home for a `Microsoft.WebApplication.targets`
replacement if the Web Site project model lands
([web-site-vs-wap-project-models](web-site-vs-wap-project-models.md)).

Costs that kept it out of M2: SDK resolution and version pinning move to
`global.json` (a second version surface beside PackageReference), defaults
ordering is subtler than package targets (SDK props run before the project
body), and the current two-package contract had not yet proven its property
names stable. Revisit once a second real application has been ported and the
knob set has settled.
