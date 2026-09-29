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

## Measured WAP publish (2026-09-12, alpha packaging)

`eng/external-consumer.sh` publishes the stock template Host from the
`0.1.0-alpha.1` candidate packages outside the checkout. `dotnet publish -c
Release` produced `site-publish/` holding the site content (markup, masters,
`Bundle.config`, `Content/`, `Scripts/`, `favicon.ico`, `Global.asax`; no
`*.cs`, designer files or `Web.*.config`), `bin/` with the host payload (79
files on macOS arm64), and one `web.config` with `Web.Release.config` applied
first and `Web.Rehost.config` after it, so `debug="true"` is gone and the
`<runtime>` and `<system.codedom>` sections are removed. Runtime page
compilation still happens in the published process; nothing is precompiled.
This is a measurement, not a production-deployment promise.

## Open

- Define deterministic WAP publish payload and package-only consumption.
  The measured shape above is the current behavior to decide against.
- Decide whether checked-in designer files are frozen inputs or regenerate
  without Visual Studio.
- Add an explicit Web Site publish mode carrying runtime-owned source,
  including `App_Code`, `App_GlobalResources`, and `CodeFile` files.
- Decide whether the WAP build contract belongs in a
  `Rehost.Web.Sdk` package.

## Done when

Fresh package consumers publish both models without source omission or double
compilation, and tests prove each ownership rule.
