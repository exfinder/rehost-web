# eShopLegacyWebForms

Microsoft's eShopLegacyWebForms catalog manager — a .NET Framework 4.7.2 Web
Application Project with Autofac dependency injection, EF6 behind a mock-data
switch, log4net, classic `MapPageRoute` URLs and Application Insights — running
on the ported runtime from packages. The Milestone 2 application.

Nothing here is a support claim; [`docs/compatibility.md`](../../docs/compatibility.md)
remains the only one. The pre-import analysis is
[`docs/research/eshoplegacywebforms-portability.md`](../../docs/research/eshoplegacywebforms-portability.md).

## Provenance

| Property | Value |
| --- | --- |
| Upstream | `dotnet-architecture/eShopModernizing`, `eShopLegacyWebFormsSolution/src/eShopLegacyWebForms` |
| Imported | `e2cbbe8`, 2026-08-25 |

## Layout

| Folder | Role |
| --- | --- |
| `eShopLegacyWebForms/` | The frozen 4.7.2 WAP. Never modified. |
| `eShopLegacyWebForms.App/` | The port of the app assembly: compiles the legacy folder's `*.cs` into `eShopLegacyWebForms.dll`, plus the `jquery`/`bootstrap` script-mapping shim. |
| `eShopLegacyWebForms.Host/` | The process: a ~20-line Kestrel host and the app's `Web.Rehost.config`. |
| `Autofac.Integration.Web/` | Recompile of `Autofac.Web` 4.0.0: upstream ships `net45` only and binds Framework's strong-named `System.Web`. |

## Commands

```text
dotnet build apps/eShopLegacyWebForms/eShopLegacyWebForms.slnx
dotnet run --project apps/eShopLegacyWebForms/eShopLegacyWebForms.Host
# http://127.0.0.1:5083/ (add `-- --urls <url>` to change)

apps/eShopLegacyWebForms/smoke.sh                    # against the default URL
eng/app-linux-smoke.sh eShopLegacyWebForms 5083      # the same, in a Linux container
```

No database and no network: `UseMockData=true` (`Web.config:15`) keeps
`CatalogDBContext` and its `(localdb)\MSSQLLocalDB` connection string unreached.

## packages.config → PackageReference

| `packages.config` | Here | Note |
| --- | --- | --- |
| `Autofac` 4.9.1 | same package, unchanged | Pure managed |
| `Autofac.Web` 4.0.0 | `Autofac.Integration.Web/` | The one library recompile |
| `EntityFramework` 6.2.0 | same package at 6.5.2 | 6.3+ ships `netstandard2.1`; unreached under mock data |
| `log4net` 2.0.10 | same package, unchanged | Pure managed |
| `Microsoft.AspNet.Web.Optimization`, `.WebForms`, `WebGrease`, `Antlr`, `Newtonsoft.Json` | `Rehost.AspNet.Web.Optimization`, `.WebForms` | The `<controls>` assembly rewrite is in this app's XDT |
| `Microsoft.AspNet.ScriptManager.MSAjax`, `.WebForms` | `Rehost.AspNet.ScriptManager.MSAjax` | Registers `MsAjaxJs`, `WebFormsJs` and the MicrosoftAjax names |
| `Microsoft.AspNet.FriendlyUrls`, `.Core` | `Rehost.AspNet.FriendlyUrls` | Reached through `IsMobileView` on the view switcher |
| `AspNet.ScriptManager.jQuery` 3.3.1, `.bootstrap` 4.3.1 | `eShopLegacyWebForms.App/PreApplicationStartCode.cs` | Names only, so the definitions are re-registered by hand |
| `Microsoft.AspNet.SessionState.SessionStateModule` | dropped by XDT | Binds Framework's strong-named `System.Web`; the built-in module is identical for `mode="InProc"` |
| `Microsoft.ApplicationInsights.*`, `Microsoft.AspNet.TelemetryCorrelation` | dropped by XDT | Same binding, and the collectors are Windows-only |
| `Microsoft.CodeDom.Providers.DotNetCompilerPlatform`, `Microsoft.Net.Compilers` | dropped | `<system.codedom>` is removed wholesale; the compile surface is the port's own |
| `jQuery`, `bootstrap`, `popper.js`, `Modernizr`, `Respond` | unchanged content | Committed under `Scripts/`, `Content/` |

## web.config

`eShopLegacyWebForms.Host/Web.Rehost.config` replaces the package default
wholesale, so it repeats the default's `<runtime>` and `<system.codedom>`
removals and the Optimization `<controls>` retarget, then drops two module
groups: the `Session` swap and the three Application Insights /
`TelemetryCorrelation` rows. A module row whose type will not load fails its
URLs with the entry named.

Left as authored: `debug="true"`, `requestValidationMode="2.0"`,
`<sessionState mode="InProc" />`, the `CatalogDBContext` connection string, the
`<entityFramework><defaultConnectionFactory>` naming `LocalDbConnectionFactory`,
and the two Autofac module rows (`ContainerDisposal`, `PropertyInjection`),
which resolve against the recompile.

`<%$ RouteUrl:… %>` on `Default.aspx` resolves through the baseline
`<expressionBuilders>` registration in the port's own
[`rehost.web.config`](../../src/Rehost.Web/configs/rehost.web.config).

## Covered by `smoke.sh`

Routed home page with mock catalog data and the `esh-pager`; `/Catalog/Create`,
`/Catalog/Details/1`, `/Catalog/Edit/1` and `/Catalog/Delete/1` over
`MapPageRoute`; a `GetRouteUrl`-emitted Edit link; `/About.aspx` and
`/Contact.aspx`; `/Pics/1.png`; the `MsAjaxJs` bundle asserted on
`Sys.Application` and the `WebFormsJs` bundle on `__doPostBack`.

GET only, on all three platforms.

## Not exercised

The Catalog create/edit/delete postbacks and their validators, the EF6 and
LocalDB path behind `UseMockData=false`, `UseCustomizationData`,
`Site.Mobile.Master` and `ViewSwitcher.ascx`, and log4net output (`Global.asax.cs`
logs through it; the application ships no appender configuration).
