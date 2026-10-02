# Assembly graph

## Dependency direction

Framework permits circular assembly references by compiling against previously
built binaries; MSBuild cannot build that source graph. Companion assemblies
therefore depend on Rehost.Web, and the runtime depends on none of them.

WebServicesSection must live beside the protocol machinery whose internal factories
it owns. The runtime's typed getter is excluded; configuration resolves the section
lazily. Runtime execution can therefore load ASMX machinery when reached without
introducing a compile-time cycle.

## Deployment promises

A compilation assembly entry is a hard load during first-request initialization.
A pages/controls prefix can load every registered assembly even for a basic control.
A missing dependency there breaks unrelated pages. A handler with validate=false
resolves its type only when its URL is requested, keeping that failure path-local.

Framework's GAC fulfilled those promises. The Rehost.Web package bundles runtime,
ApplicationServices, Extensions, Services and Infrastructure with root configuration
and their external closure. The five retain separate assemblies/projects and are
not separately published. Satellites reference component projects with
PrivateAssets=all and the bundle for their public dependency edge. Runtime targets
ship in buildTransitive so hosts receive them through application dependencies.

Infrastructure joins the bundle because companions and applications compile against
its public types, though configuration does not name it.

## Naming

- Replace leading System/Microsoft with Rehost in assembly and package identities;
  preserve upstream namespaces. Authored code uses Rehost namespaces.
- Derive package and assembly names separately: Microsoft.AspNet.Web.Optimization
  carries System.Web.Optimization; its replacements are Rehost.AspNet.Web.Optimization
  and Rehost.Web.Optimization. Web API follows the same rule.
- Original-free projects use Rehost.Web names, including AspNetCore and Templates.
- A pack-only project bundles WebPages/Razor/Deployment because their dependency
  graph cannot make one component own the complete package.
- Rehost.Web.Package packs the runtime bundle. The runtime project's package ID
  Rehost.Web.Runtime is restore-only, avoiding two projects with package ID Rehost.Web.
- Keep the two ScriptManager package identities: MSAjax registers MsAjaxBundle and
  MicrosoftAjax names; WebForms registers WebFormsBundle.
