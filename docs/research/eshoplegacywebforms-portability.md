# eShopLegacyWebForms portability analysis

Preliminary, pre-import analysis of Microsoft's eShopLegacyWebForms — the
Milestone 2 application. Source read at
`../../../eShopModernizing/eShopLegacyWebFormsSolution/src/eShopLegacyWebForms`
(read-only clone, nothing modified). Nothing here is a support claim; the
[compatibility map](../compatibility.md) remains the only one. Statements the
current evidence cannot decide are marked **unverified**.

## Application shape

| Property | Value | Source |
| --- | --- | --- |
| Project model | Web Application Project (`{349c5851-…}` guid), `OutputType Library` | `eShopLegacyWebForms.csproj:14-15` |
| Target framework | `v4.7.2`; `<compilation targetFramework="4.7.2">`, `<httpRuntime targetFramework="4.6.1">` | `csproj:19`, `Web.config:27-28` |
| Language version | none set; `<system.codedom>` passes `/langversion:default` | `Web.config:95-96` |
| Code-behind | Checked-in `.designer.cs` for every page, master and control; `Compile`/`Content` casing matches disk | `csproj:320-404` |
| Pages | 4 top-level `.aspx`, 4 `Catalog/*.aspx`, `Site.Master`, `Site.Mobile.Master`, `ViewSwitcher.ascx`, `Global.asax` | tree |
| Hosting metadata | `UseIISExpress`, classic-pipeline flag empty | `csproj:20-25` |

Nothing unusual for the sidecar contract. `App_Start/` is a plain folder here,
and the runtime targets only strip `App_Code`/`App_Data`
(`src/Rehost.Web.Package/build/Rehost.Web.targets:31`), so
`BundleConfig.cs` and `RouteConfig.cs` compile into the app assembly as they do
on Framework.

Two shape notes carry into the port build: the GAC `<Reference>` block
(`csproj:111-185`) names `System.Web.DynamicData`, `System.Web.Entity`,
`System.EnterpriseServices`, `System.Management`, `System.Drawing` and
`System.Data.DataSetExtensions`, none of which application code reaches — they
disappear with the GAC. And `debug="true"` stays in the shipped `Web.config`
(`Web.config:27`), so bundling and ScriptManager run their debug-expansion
paths, the same ones the frozen templates exercise.

## Dependency inventory and classification

Classes: **A** provided by the port, **B** consumable from nuget.org after
recompile, **C** real gap (no port surface and no consumable package), **D**
Windows- or platform-bound.

### System.Web surface reached by application code

| Surface | Class | Evidence |
| --- | --- | --- |
| Pages, masters, user control, code-behind, `Global.asax` | A | [map](../compatibility.md): pre-application start / `Global.asax`, `.aspx` GET, master pages and user controls — Supported |
| `HttpApplication` events (`Application_Start`, `Application_BeginRequest`, `Session_Start`) | A | map: async module events Supported; `SessionStateUtility.RaiseSessionStart` in `src/System.Web.ReferenceSource/State/SessionStateUtil.cs` |
| `System.Web.Routing` `MapPageRoute`, `Page.RouteData`, `GetRouteUrl` | A (scope unverified) | `src/System.Web.ReferenceSource/Routing/PageRouteHandler.cs`; the map's routing row is written for Friendly URLs, and classic page routing carries no separate claim |
| `<%$ RouteUrl:RouteName=… %>` expression builder | A (unverified) | `src/System.Web.ReferenceSource/Compilation/RouteUrlExpressionBuilder.cs`; used on `Default.aspx:7` |
| `ListView` + `ItemType` two-way expressions | A | map: `ListView`/`DataPager` closure landed with the Identity application; Dynamic Data hook inactive rather than fatal (ledger P69) |
| `DropDownList` `SelectMethod` + `ItemType` model binding | A (unverified) | `src/System.Web.ReferenceSource/UI/WebControls/DataBoundControl.cs:144-146`; map says other controls are Unassessed |
| `Page.ModelState` / `TryUpdateModel` path | A | `src/System.Web.ReferenceSource/UI/Page.cs:468`, full `ModelBinding/` folder present |
| `RequiredFieldValidator`, `RangeValidator` (unobtrusive mode) | A, with a caveat | see the jquery-mapping note below |
| `Session["…"]` read on every page through the master | A | map: session state Partial — InProc works |
| `Response.Redirect("~")`, `<a runat="server" href="~">` | A | map: path-taking APIs Supported |
| `HttpUtility.UrlEncode`, `Request.RawUrl`, `Request.UserAgent` | A | map: request surfaces Supported |
| `HostingEnvironment.ApplicationPhysicalPath` | A | `src/System.Web.ReferenceSource/Hosting/HostingEnvironment.cs:1391`; reached only on the SQL path |
| `System.Web.Optimization` (`Scripts.Render`, `BundleTable`) and `webopt:BundleReference` | A (Partial) | map: Optimization/WebForms and WebGrease — Partial |
| `ScriptManager` with named and `Assembly="System.Web"` script references | A (Partial) | map: `System.Web.Extensions`/ScriptManager Supported; named-definition coverage below |
| `Microsoft.AspNet.FriendlyUrls.Resolvers.WebFormsFriendlyUrlResolver.IsMobileView` | A | `Rehost.AspNet.FriendlyUrls`; map: Friendly URLs Partial |

### packages.config

| Package | Class | Disposition |
| --- | --- | --- |
| `Microsoft.AspNet.Web.Optimization` 1.1.3, `.WebForms`, `WebGrease` 1.6, `Antlr` 3.5.0.2 | A | `Rehost.AspNet.Web.Optimization`, `.WebForms`; the `<controls>` assembly rewrite is already in the default XDT (`src/Rehost.Web.AspNetCore/build/Web.Rehost.config:13-15`) |
| `Microsoft.AspNet.FriendlyUrls` + `.Core` 1.0.2 | A | `Rehost.AspNet.FriendlyUrls` |
| `Microsoft.AspNet.ScriptManager.MSAjax` / `.WebForms` 5.0.0 | A | `Rehost.AspNet.ScriptManager.MSAjax` registers `MsAjaxBundle` and the 11 MicrosoftAjax names, `Rehost.AspNet.ScriptManager.WebForms` registers `WebFormsBundle` |
| `AspNet.ScriptManager.bootstrap` 4.3.1 | **C** | Registers the `bootstrap` `ScriptResourceMapping` name that `Site.Master:27` asks for. No port registrar exists; backlog already records that the Bundles package "registers names only" for the MsAjax/WebForms set |
| `AspNet.ScriptManager.jQuery` 3.3.1 | C (low risk) | Registers `jquery`. Also unregistered in the port, yet both frozen templates use `<asp:ScriptReference Name="jquery" />` and pass, so the reference is non-fatal today; the definition itself is still absent |
| `Microsoft.CodeDom.Providers.DotNetCompilerPlatform` 2.0.1, `Microsoft.Net.Compilers` 2.10.0 | A (dropped) | The runtime owns compiler selection; `<system.codedom>` is removed by the default XDT (`Web.Rehost.config:9`) |
| `Microsoft.Web.Infrastructure` 1.0.0 | A (dropped) | Same disposition as the Identity application |
| `Autofac` 4.9.1 | B | Modern Autofac ships `netstandard2.0`; the API the app uses (`Module`, `ContainerBuilder`, `RegisterType`, `SingleInstance`, `InstancePerLifetimeScope`) is stable |
| `Autofac.Web` 4.0.0 (`Autofac.Integration.Web`) | **C, load-bearing** | Binds Microsoft's strong-named `System.Web`; every published build is `net45`+. Must be recompiled under Rehost identity, exactly as Katana was. It supplies `ContainerDisposalModule` and `Forms.PropertyInjectionModule`, which is how every page receives `CatalogService` |
| `Microsoft.AspNet.SessionState.SessionStateModule` 1.1.0 | **C** | Binds `System.Web` and `ISessionStateModule`. The interface exists here (`src/System.Web.ReferenceSource/State/ISessionStateModule.cs:17`), so recompiling is open; dropping the `<remove name="Session"/>`/`<add …>` pair by XDT and falling back to the built-in InProc module is the cheaper substitution |
| `Microsoft.AspNet.TelemetryCorrelation` 1.0.5 | **C** | Binds `System.Web`; registered as a module. Recompile or remove |
| `Microsoft.ApplicationInsights` 2.9.1 | B | Core SDK is `netstandard`; nothing in application code calls it |
| `Microsoft.ApplicationInsights.Web` 2.9.1 (`Microsoft.AI.Web`) | **C** | Binds `System.Web`; no modern equivalent for the classic pipeline. Registered as a module, so it decides whether requests run |
| `Microsoft.ApplicationInsights.PerfCounterCollector` 2.9.1 | **D** | Pulls `System.Diagnostics.PerformanceCounter`, Windows-only on modern .NET; `ApplicationInsights.config:40-59` registers `PerformanceCollectorModule` and QuickPulse |
| `Microsoft.ApplicationInsights.WindowsServer` + `.TelemetryChannel` 2.9.1 | **D** | Azure role-environment and heartbeat collectors; `ApplicationInsights.config:5-7` |
| `Microsoft.ApplicationInsights.Agent.Intercept` 2.4.0, `.DependencyCollector` 2.9.1 | C/D | Framework profiler interception; not portable as shipped |
| `System.Diagnostics.PerformanceCounter` 4.5.0 | **D** | Windows-only on modern .NET. Reached only through Application Insights |
| `EntityFramework` 6.2.0 | B | EF 6.4+/6.5 from nuget.org, the Identity-application precedent. Only the SQL path exercises it |
| `log4net` 2.0.10 | B | `netstandard2.0` build exists. Configuration hazards below |
| `Newtonsoft.Json` 12.0.1 | B | Optimization/WebGrease dependency |
| `System.Buffers`, `.Memory`, `.Numerics.Vectors`, `.Runtime.CompilerServices.Unsafe`, `.Threading.Channels`, `.Threading.Tasks.Extensions`, `.IO.Pipelines`, `.IO.Compression(.ZipFile)`, `.Diagnostics.DiagnosticSource`, `Pipelines.Sockets.Unofficial` | B | In-box on net10 or restorable; all transitive under Application Insights |
| `jQuery`, `bootstrap`, `popper.js`, `Modernizr`, `Respond` | — | Content only; the files are committed under `Scripts/` and `Content/` |

### Web.config

| Entry | Class | Note |
| --- | --- | --- |
| `<validation validateIntegratedModeConfiguration="false" />` (`:71`) | A | Waives the classic `<httpModules>` block at `:38-43`; `ClassicSectionValidation.Validate` returns early and the section stays dead text (`src/Rehost.Web/Compatibility/IisConfig/ClassicSectionValidation.cs:21-46`). No activation refusal |
| `<system.webServer><modules>` (`:72-81`) | see above | The effective list: `ContainerDisposal`, `PropertyInjection`, `TelemetryCorrelationHttpModule`, `ApplicationInsightsWebTracking`, and `Session` replaced by `SessionStateModuleAsync`. Map: modules Supported, including mutations and ordering |
| `<sessionState mode="InProc" />` (`:29`) | A | Map: session state Partial — InProc supported |
| `<httpRuntime targetFramework="4.6.1" requestValidationMode="2.0" />` (`:28`) | A / unverified | ≥4.5 satisfies the Required row. `requestValidationMode="2.0"` has surface (`HttpRequest.cs`, `Configuration/HttpRuntimeSection.cs`) but no compatibility claim |
| `<pages><controls>` webopt registration (`:35-37`) | A | Default XDT rewrites the assembly name |
| `<globalization culture/uiCulture="en-US" />` (`:44`) | A | — |
| `<runtime><assemblyBinding>` (`:46-69`) | A (dropped) | Default XDT removes `<runtime>` wholesale |
| `<connectionStrings>` LocalDb (`:12`) | **D** | SQL path only; the Identity application already set the precedent that the connection string is the one app-visible change |
| `<entityFramework><defaultConnectionFactory type="…LocalDbConnectionFactory">` (`:83-91`) | **D** | Parsed lazily by EF; unreached with mock data |
| `<system.codedom>` (`:93-98`) | A (dropped) | Removed by the default XDT |
| `<appSettings>` `UseMockData=true`, `UseCustomizationData=false` (`:14-17`) | A | Read through `ConfigurationManager.AppSettings`; map: static `ConfigurationManager` Supported |

`ApplicationInsights.config` and `log4Net.xml` are sidecar configuration files
of their own libraries, not `System.Web` configuration; neither is read by the
runtime.

`Bundle.config` is dead weight — the Optimization manifest default path is
`~/bundle.config` (`src/System.Web.Optimization.ReferenceSource/BundleManifest.cs:18`)
and nothing calls `ReadBundleManifest`; bundles come from `BundleConfig.cs`.

### Global.asax and App_Start wiring

`Global.asax.cs:29-36` runs, in order: `RouteConfig.RegisterRoutes`,
`BundleConfig.RegisterBundles`, Autofac container build, and — only when
`UseMockData` is false — `Database.SetInitializer`.

- `Global : HttpApplication, IContainerProviderAccessor` (`:17`) is the hook the
  Autofac modules read; without the recompiled `Autofac.Integration.Web` there
  is no injection and every page's `CatalogService` property stays null.
- `Session_Start` (`:41-45`) writes `Environment.MachineName` and
  `DateTime.Now`; `Site.Master.cs:17` reads both on **every** page render, so
  session is on the critical path of every journey.
- `Application_BeginRequest` (`:69-77`) sets `log4net`
  `LogicalThreadContext.Properties` and touches
  `Trace.CorrelationManager.ActivityId` (`:84-89`). Both are logical-call-context
  mechanisms; log4net's `netstandard` build uses `AsyncLocal` rather than
  remoting `CallContext` — **unverified** against the port's illogical-CallContext
  boundary, but the port's own restriction ("application access to remoting
  `CallContext` — Unsupported") concerns publishing the compatibility type, not
  `AsyncLocal`.
- `RouteConfig.cs:9-38` registers six `MapPageRoute` entries and **never calls
  `EnableFriendlyUrls`**. Friendly URLs routing is therefore not active in this
  application: `ViewSwitcher.ascx.cs:31-37` finds no switch-view route and hides
  itself, and `Site.Mobile.Master` is unreachable. Only
  `WebFormsFriendlyUrlResolver.IsMobileView` is reached, so the FriendlyUrls
  assembly is needed for compilation, not for behavior.
- `BundleConfig.cs:39-45` registers a `respond` script definition itself, which
  covers one of the three non-port ScriptManager names.

## Windows-only, and where the boundary falls

| Item | Reached by | Disposition |
| --- | --- | --- |
| `(localdb)\MSSQLLocalDB` connection string and `LocalDbConnectionFactory` | SQL path only | Out of the portable contract; substitute a reachable SQL Server, as `WebFormsIdentityApplication` did. Milestone 3 |
| `System.Diagnostics.PerformanceCounter` (AI `PerformanceCollectorModule`, QuickPulse) | Application Insights modules | Not portable; remove with the AI registration |
| AI `WindowsServer` role-environment/heartbeat collectors and `Agent.Intercept` | `ApplicationInsights.config` | Same |
| `System.EnterpriseServices`, `System.Management`, `System.Drawing`, `System.Web.DynamicData`, `System.Web.Entity` | csproj `<Reference>` only | Unreached; dropped with the GAC |
| `SELECT NEXT VALUE FOR …` sequences, `.sql` seed scripts, `ZipFile.ExtractToDirectory` into `Pics/` | SQL path only | SQL Server engine dependency, not an OS dependency |

Application code contains **no** P/Invoke, registry access, `EventLog`, COM,
DPAPI or WMI use. The only machine-bound call is `Environment.MachineName`,
which is portable.

## Mock-data vs SQL-data split

`UseMockData=true` is the shipped default (`Web.config:15`). With it:

- Autofac binds `CatalogServiceMock` (`Modules/ApplicationModule.cs:18-23`),
  which serves twelve items straight from
  `Models/Infrastructure/PreconfiguredData.cs` — pure BCL, no I/O.
- `ConfigDataBase` returns without touching EF (`Global.asax.cs:59-67`).
- `CatalogDBContext` and `CatalogDBInitializer` are still *registered*
  (`ApplicationModule.cs:31-35`), so the `EntityFramework` assembly must load,
  but nothing resolves them.

Dependencies that matter only on the SQL path: `EntityFramework` runtime
behavior, `System.Data.SqlClient` (`Services/CatalogService.cs:5`), the LocalDb
connection string and connection factory, `CatalogDBInitializer` (CSV/zip
customization data, `HostingEnvironment.ApplicationPhysicalPath`,
`AppDomain.CurrentDomain.BaseDirectory`), and `CatalogItemHiLoGenerator`'s raw
`SELECT NEXT VALUE FOR catalog_hilo` (`Models/CatalogItemHiLoGenerator.cs:22`).

Mock mode also changes CRUD semantics: `CatalogServiceMock` is a singleton over
an in-memory `List<CatalogItem>`, so create/edit/delete mutate process state and
are lost on restart. That is the right shape for a deterministic journey, but a
smoke script must not assume persistence across a process replacement.

## Cross-platform hazards in the frozen application

These are application-authored defects, not port gaps. They are recorded here
because they will surface as Linux/macOS-only failures and must not be
misattributed to the runtime.

| Hazard | Evidence | Effect |
| --- | --- | --- |
| log4net config filename case | `Properties/AssemblyInfo.cs:37` names `log4net.xml`; the file on disk is `log4Net.xml` | On a case-sensitive filesystem the configurator finds nothing. Separately, `XmlConfigurator(ConfigFile=…)` resolves against the base directory, while the file is staged as site content — so logging is likely inert on every OS. No journey impact; `_log.Info` becomes a no-op |
| Backslash log path | `log4Net.xml:7` — `file value="logFiles\myapp.log"` | Off Windows this creates a file literally named `logFiles\myapp.log` |
| MsAjax bundle path case | `App_Start/BundleConfig.cs:23-26` includes `~/Scripts/WebForms/MsAjax/…`; the folder on disk is `Scripts/WebForms/MSAjax` | Covered by P57: bundle resolution defaults to `HostingEnvironment.VirtualPathProvider` (`BundleTable.cs:49`), whose MapPath exits through `CanonicalCasePath` (`1a2b32f`), folding the miss to the true casing. The same mismatch exists in `apps/WebFormsApplication`; no case-sensitive test exercises a mismatched bundle include end-to-end |
| Backslash script paths | `Models/Infrastructure/CatalogDBInitializer.cs:19-21`, `:334` | SQL path only; `Path.Combine(BaseDirectory, @"Models\Infrastructure\…")` produces a literal-backslash filename off Windows |
| Case-mismatched `CodeBehind` attributes | `Site.Master:1` (`Site.master.cs`), `Site.Mobile.Master:1` | Compile-time metadata in a WAP; the runtime resolves `Inherits`, so this is inert. `MasterPageFile="~/Site.Master"` matches disk |

## Verdict

**Expected to run unchanged.** The browse journey's whole rendering stack is
inside the Milestone 1 closure: page and master compilation, the `ListView` +
`ItemType` data path, postback, validators' server side, InProc session,
`ConfigurationManager` app settings, static files under `Pics/`, `images/` and
`fonts/`, Optimization bundles in debug mode, and ScriptManager. The mock data
service is pure BCL. The classic `<httpModules>` block, the `<runtime>` binding
redirects and `<system.codedom>` — the three things that look alarming in a
frozen `Web.config` — are all already solved: waived by
`validateIntegratedModeConfiguration="false"`, and removed by the default
`Web.Rehost.config`.

**Needs port or sidecar work.** Four `packages.config` entries bind Microsoft's
`System.Web` and have no modern publication: `Autofac.Web`,
`Microsoft.AspNet.SessionState.SessionStateModule`,
`Microsoft.AspNet.TelemetryCorrelation`, and `Microsoft.ApplicationInsights.Web`.
Each is either a Katana-style recompile or an XDT removal. Beyond that, the new
runtime surface eShop reaches past the templates is small and specific: classic
`MapPageRoute` page routing with `RouteData`, the `RouteUrl` expression builder,
`SelectMethod`/`ItemType` model binding on `DropDownList`, `Page.ModelState`, and
`requestValidationMode="2.0"`. Every type exists in `src/`; none carries a
compatibility claim.

**Windows-only, needs a stated boundary.** LocalDb (SQL path, Milestone 3) and
the Application Insights performance-counter/WindowsServer collectors. Neither
is reachable in the mock-data journey, so both can be handled by a stated
boundary plus a sidecar substitution rather than by port work.

### Gap list, ordered by likelihood of blocking the mock-data journeys

1. **`Autofac.Integration.Web` has no portable build.** Without
   `PropertyInjectionModule`, `CatalogService` is null on every page and every
   journey NREs at `Page_Load`. Recompile under Rehost identity.
2. **Application Insights and TelemetryCorrelation modules are registered in
   `<modules>`.** A module row whose type will not load fails its URLs with the
   entry named (MH22a), so this blocks every request until it is removed by XDT
   or the assemblies are recompiled. `Microsoft.AI.Web` binds `System.Web`;
   `PerfCounterCollector`/`WindowsServer` are Windows-bound regardless.
3. **The `Session` module is swapped for
   `SessionStateModuleAsync`.** `Site.Master.cs:17` reads session on every
   render. Either recompile the package against the port's `ISessionStateModule`
   or XDT-drop the `<remove>`/`<add>` pair back to the built-in InProc module.
4. **`bootstrap` (and `jquery`) `ScriptResourceMapping` names are
   unregistered.** `Site.Master:26-28` is on every page. `respond` the
   application registers itself; the other two came from packages the port
   replaces with `Rehost.AspNet.ScriptManager.MSAjax` and `.WebForms`, which
   register only the MsAjax/WebForms names. Severity is **unverified**: the frozen templates
   already reference `jquery` unregistered and pass, so an unresolved name is
   probably not fatal — but that has never been measured, and `bootstrap` is new.
5. **Classic page routing breadth.** Six `MapPageRoute` entries, `Page.RouteData`
   on four pages, `GetRouteUrl` on every catalog row, and `<%$ RouteUrl:…%>` on
   the create link. `PageRouteHandler` and `RouteUrlExpressionBuilder` are
   imported; the map's routing claims are written for Friendly URLs only.
6. **Model binding on the create/edit forms.** `SelectMethod` +`ItemType` on
   `DropDownList` and `Page.ModelState.IsValid` gate every write
   (`Catalog/Create.aspx:29-33`, `Create.aspx.cs:32`). The map covers
   `ListView`/`DataPager` and calls other controls Unassessed.
7. **`requestValidationMode="2.0"`** combined with `ValidateRequest="false"` on
   the two write pages. Surface exists; no claim.
8. **`debug="true"` Optimization and ScriptManager expansion breadth**, plus the
   `MsAjax`/`MSAjax` casing mismatch on case-sensitive filesystems.
9. **Static-content MIME coverage** for `fonts/*.woff2|eot|svg` through the
   IIS-derived static map.
10. **Friendly URLs is inert here** — routes are never enabled, so `ViewSwitcher`
    hides itself and `Site.Mobile.Master` is unreachable. Lowest risk, but it
    means the mobile-master path the templates prove is not exercised by eShop.

## Sources

- Application: `eShopModernizing/eShopLegacyWebFormsSolution/src/eShopLegacyWebForms`
  (`Web.config`, `eShopLegacyWebForms.csproj`, `packages.config`,
  `Global.asax.cs`, `App_Start/*.cs`, `Modules/ApplicationModule.cs`,
  `Services/*.cs`, `Models/**`, `Catalog/*.aspx*`, `Site*.Master*`,
  `ViewSwitcher.ascx*`, `ApplicationInsights.config`, `log4Net.xml`)
- Support claims: [compatibility map](../compatibility.md)
- Unresolved work referenced: [backlog](../backlog.md)
- Milestone 1 precedents:
  [Identity application findings](webforms-identity-application-gaps.md),
  [`apps/WebFormsApplication/DEVELOPMENT.md`](../../apps/WebFormsApplication/DEVELOPMENT.md),
  [`apps/WebFormsIdentityApplication/DEVELOPMENT.md`](../../apps/WebFormsIdentityApplication/DEVELOPMENT.md)
