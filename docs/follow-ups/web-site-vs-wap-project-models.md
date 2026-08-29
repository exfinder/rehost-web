# Web Site and Web Application Project packaging

Web Application Projects (WAP) are the primary target. Both frozen Visual
Studio templates build from packages and pass their three-platform journeys;
current support lives in the [compatibility map](../compatibility.md).

## Model boundary

- WAP: MSBuild compiles `CodeBehind`, designer partials, resources, and
  `Global.asax.cs` into the application assembly; runtime resolves `Inherits`.
- Web Site: runtime compiles `CodeFile`, `App_Code`, resources, and inline
  `Global.asax`; source must remain in the published site.

Runtime targets own SDK exclusions and the
`RehostAppContentRoot`/`RehostSiteContentRoot` sidecar contract. Hosting targets
own staging, XDT transformation, and publish layout. One target cannot infer
which owner should compile or publish `*.aspx.cs` safely.

The AjaxControlToolkit sample site
([`apps/AjaxControlToolkitSampleSite`](../../apps/AjaxControlToolkitSampleSite/README.md))
runs the Web Site model end-to-end: `App_Code`, `CodeFile` pages, inline
`Global.asax`, and `.asmx` services all compile at runtime on macOS and Linux.
The gap is confined to staging: the hosting targets exclude `**/*.cs`, so its
Host carries a local `StageWebSiteSources` target — the reference input for the
publish-mode decision below. `App_GlobalResources` and Web Site
publish/precompile remain unexercised.

## Open

- Define deterministic WAP publish payload and package-only consumption.
- Decide whether checked-in designer files are frozen inputs or regenerate
  without Visual Studio.
- Add an explicit Web Site publish mode carrying runtime-owned source,
  including `App_Code`, `App_GlobalResources`, and `CodeFile` files.
- Decide whether the WAP build contract belongs in a
  `Rehost.WebForms.Sdk` package.

## Done when

Fresh package consumers publish both models without source omission or double
compilation, and tests prove each ownership rule.
