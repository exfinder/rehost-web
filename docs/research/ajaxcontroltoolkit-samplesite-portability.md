# AjaxControlToolkit.SampleSite portability analysis

Preliminary, pre-import analysis of the AJAX Control Toolkit sample site and the
toolkit assemblies it cannot run without. Source read at
`../../../AjaxControlToolkit` (read-only clone at `0769b45`, the archived
v20.1 tree, nothing modified). This is exploratory: no milestone owns this
application. Nothing here is a support claim; the
[compatibility map](../compatibility.md) remains the only one. Statements the
current evidence cannot decide are marked **unverified**.

Unlike eShop, this application is inseparable from a large third-party control
library. The sample site itself is thin — 51 `.aspx` pages that mostly declare
markup. Almost all of the portability question lives in
`AjaxControlToolkit.dll` (350 `.cs` files, 538 `[assembly: WebResource]`
declarations, 276 embedded scripts, 54 stylesheets, 170 images), which would
have to be recompiled under Rehost identity the way Katana and
`Autofac.Integration.Web` were.

## Application shape

| Property | Value | Source |
| --- | --- | --- |
| Project model | **Web Site** (`{E24C65DC-…}`, `http://localhost:49290`), not a WAP | `AjaxControlToolkit.sln:21-24` |
| Target framework | `.NETFramework,Version=v4.0`; `<compilation targetFramework="4.0">`; **no `<httpRuntime>` element at all** | `sln:24`, `Web.config:26` |
| Trust | `<trust level="Medium"/>` | `Web.config:39` |
| Code model | `CodeFile=` + `App_Code/` (6 files) + inline `<%@ Application Language="C#" %>` `Global.asax` | tree |
| Pages | 51 `.aspx` under one folder per control, two masters (`Layout.master` → `Samples.master`), 3 `.asmx` | tree |
| Project references | `AjaxControlToolkit.dll`, `AjaxControlToolkit.HtmlEditor.Sanitizer.dll`, `AjaxControlToolkit.StaticResources.dll` | `sln:25` |
| `bin/` | six `.dll.refresh` stubs only — the real assemblies come from restore/build | `bin/` |

Two shape facts dominate everything below.

**It is a Web Site, not a WAP.** The port's map calls the Web Site
runtime-compilation model **Partial** ("consumer build/publish packaging remains
incomplete"), and
[project models](../follow-ups/web-site-vs-wap-project-models.md) still lists
"Add an explicit Web Site publish mode carrying runtime-owned source, including
`App_Code`, `App_GlobalResources`, and `CodeFile` files" as open. Both frozen
Milestone 1 applications and eShop are WAPs. This application would be the
port's first real Web Site consumer, so the packaging work is a prerequisite,
not a detail.

**Static resources are build outputs that do not exist in the clone.**
`Scripts/AjaxControlToolkit/` and `Content/AjaxControlToolkit/` — referenced by
name from `Layout.master:10,17` on every page — are produced by the
`AjaxControlToolkit.LinkStaticResources` post-build tool
(`AjaxControlToolkit.csproj` `PostBuildEvent`), which is a Windows-only hard-link
utility (below). Importing the site means reproducing that output first.

## Toolkit library shape

| Project | Target | Notable references |
| --- | --- | --- |
| `AjaxControlToolkit` | `v4.0`, strong-named (`AjaxKey.snk`) | `System.Web`, `System.Web.Extensions`, **`System.Design`**, **`System.Web.Extensions.Design`**, `System.Drawing`, `System.Data`, `Microsoft.CSharp` |
| `AjaxControlToolkit.HtmlEditor.Sanitizer` | `v4.0` | `HtmlAgilityPack` 1.4.9, `System.Web`, project ref to the toolkit |
| `AjaxControlToolkit.StaticResources` | `v4.0` | `System.Web.Optimization` 1.1.3, `WebGrease`, `Antlr3.Runtime`, `Newtonsoft.Json`, `Microsoft.Web.Infrastructure`; carries `[assembly: PreApplicationStartMethod]` |
| `AjaxControlToolkit.LinkStaticResources` | build-time console tool | `Kernel32.dll` P/Invoke |
| `CdnBundleBuilder`, `Jasmine`, `Reference`, `ReferenceCore`, `WikiReferenceUpdater`, `VsPackage*` | build/docs/VSIX tooling | out of scope for hosting |

`[assembly: AllowPartiallyTrustedCallers]` and
`[assembly: SecurityRules(SecurityRuleSet.Level1)]`
(`AjaxControlToolkit/Properties/AssemblyInfo.cs:12-14`) are CAS-era attributes.
They are inert on modern .NET, but they pair with `<trust level="Medium"/>` in
the site config, which is not inert here (below).

## Dependency inventory and classification

Classes: **A** provided by the port, **B** consumable from nuget.org after
recompile, **C** real gap (no port surface and no consumable package), **D**
Windows- or platform-bound.

### System.Web surface reached by the sample site

| Surface | Class | Evidence |
| --- | --- | --- |
| `.aspx` + `CodeFile` + masters + `App_Code` + inline `Global.asax` | A (Partial for the model) | map: `.aspx` GET, master pages and user controls, C# `App_Code`, `Global.asax` — Supported; Web Site model — Partial |
| `Application_Start` bundle registration and `Server.MapPath` | A | map: path-taking APIs Supported |
| `System.Web.Optimization` `ScriptBundle`/`StyleBundle`, `Styles.Render` | A (Partial) with **EnableOptimizations=true** | map: Optimization/WebForms Partial; `Global.asax:15` forces the production path — see gaps |
| `ScriptManager` with `EnableScriptGlobalization` and a bundle-path `ScriptReference` | A (unverified shape) | map: ScriptManager/UpdatePanel Supported; `Layout.master:15-18`. The templates use `Name="MsAjaxBundle"`, not `Path="~/…/Bundle"`; both go through `BundleReflectionHelper` (`src/System.Web.Extensions.ReferenceSource/ui/ScriptManager.cs:1616`) |
| `UpdatePanel` async postbacks | A | map: IIS-byte-matched async deltas; 19 sample pages use `UpdatePanel` |
| Page methods (`ServiceMethod` with no `ServicePath`) | A | `CascadingDropDown/CascadingDropDown.aspx:33`, `DynamicPopulate/DynamicPopulate.aspx:55`; [extensions-compatibility](../extensions-compatibility.md) records the page-method JSON response as byte-identical to IIS |
| `.asmx` + `[ScriptService]` JSON | A | map: `.asmx` serving Supported, `[ScriptService]` JSON exercised. `App_Code/AutoComplete.cs`, `CarsService.cs`, `NumericUpDown.cs` |
| `XmlSiteMapProvider` + `SiteMapDataSource` + `Repeater` | A (unassessed) | `Web.config:28-32`, `Layout.master:33-38`; `src/System.Web.ReferenceSource/XmlSiteMapProvider.cs`, `UI/WebControls/SiteMapDataSource.cs`. Map: other controls Unassessed |
| `ObjectDataSource` + `[DataObject]` over `DataSet`/XML | A (unassessed) | `HoverMenu/HoverMenu.aspx:88`, `ReorderList/ReorderList.aspx:54`, `App_Code/TodoXmlDataObject.cs`; `src/System.Web.ReferenceSource/UI/WebControls/ObjectDataSource.cs` |
| `GridView`, `Repeater`, `CompositeControl`, `ITemplate`, `HtmlGenericControl` | A (unassessed) | present in `src/`, no claim |
| Custom `ConfigurationSection` via `WebConfigurationManager.GetSection` | A | map: static `ConfigurationManager` inside the application — Supported; `ToolkitConfig.cs:16` |
| `File.ReadAllText(Server.MapPath("~/App_Data/ControlReference/…"))` on every page render | A | `Layout.master.cs:23,31`. Runtime targets strip `App_Data` from **compilation** only (`src/Rehost.Web.Package/build/Rehost.Web.targets:31`); the 102 HTML fragments must still ship as site content |
| Client callbacks (`ICallbackEventHandler`, `GetCallbackEventReference`) | A (unassessed) | `src/System.Web.ReferenceSource/UI/ClientScriptManager.cs:262`; reached by `Rating/Rating.cs:332` and `ExtenderBase/ComponentDescriber.cs:166` |
| `System.Web`'s own `WebForms.js` etc. via `WebResource.axd` | A | The site stages no `Scripts/WebForms/` folder, so these must come from the assembly. `Rehost.Web.csproj:30-35` embeds every `RuntimeScripts/*.js` |

### System.Web surface reached by the toolkit

| Surface | Class | Evidence |
| --- | --- | --- |
| `ExtenderControl` / `ScriptControl` / `IScriptControl`, `ScriptBehaviorDescriptor`, `ScriptReference`, `ScriptDescriptor` | A (present, **unassessed**) | `ExtenderBase/ExtenderControlBase.cs:18,209,259`, `ExtenderBase/ScriptControlBase.cs:16`; all types exist under `src/System.Web.Extensions.ReferenceSource/ui/`. The map's Supported row names ScriptManager and UpdatePanel, not the extender registration path |
| `ScriptManager.ScriptResourceMapping.AddDefinition(name, assembly, definition)` with `Path`/`DebugPath`/`CdnPath` | A | `ToolkitResourceManager.cs:106-112`; `src/System.Web.Extensions.ReferenceSource/ui/ScriptResourceMapping.cs:17` |
| `ScriptResource.axd` serving embedded scripts from a **third-party** assembly | A (unverified at this scale) | 538 `[assembly: WebResource]` entries. [extensions-compatibility](../extensions-compatibility.md) pins the port's own 13 generated scripts only |
| `ClientScript.GetWebResourceUrl` / `WebResource.axd`, including `PerformSubstitution=true` CSS | A (Partial) | map: `WebResource.axd` Partial (one image round-trips). Substitution is implemented (`src/System.Web.ReferenceSource/Handlers/AssemblyResourceLoader.cs:628`); `AssemblyName.CodeBase` at `:169` is unguarded and is the open [resource timestamps](../follow-ups/web-resource-assembly-timestamps.md) item |
| `ScriptManager.RegisterClientScriptBlock`, `ClientScript.RegisterStartupScript` | A | `ToolkitResourceManager.cs:243,280,294` |
| `JavaScriptSerializer` | A | `ToolkitResourceManager.cs:287`; in the Extensions closure |
| `IHttpHandler` + `IReadOnlySessionState` at `AjaxFileUploadHandler.axd` | A | map: `system.webServer/handlers` Supported; `AjaxFileUpload/AjaxFileUploadHandler.cs:11` |
| `HttpRequest.GetBufferlessInputStream()` and `ReadEntityBodyMode` (via `dynamic`) | A | map: bufferless input Supported; `AjaxFileUpload/AjaxFileUploadHelper.cs:65,80` |
| `HttpPostedFile` stored in `Session` (InProc enforced by the control) | A | map: session state Partial — InProc works; `AsyncFileUpload/PersistentStoreManager.cs:72-75` |
| `HttpRuntime.Cache` + `CacheDependency`, `HttpRuntime.AppDomainAppPath`, `HttpContext.Current.Items` | A | `Bundling/DefaultCache.cs:11,23`, `ToolkitResourceManager.cs:94,361-375` |
| `TypeDescriptor.GetProperties` over control instances at render | A, **with an attribute hazard** | `ExtenderBase/ExtenderControlBase.cs:236`, `ExtenderBase/ComponentDescriber.cs:27` — see the design-time section |
| `Type.GetType(assemblyQualifiedName)` for the sanitizer | A (unverified) | `HtmlEditor/EditPanel.cs:50`. Plain `Type.GetType`, not `BuildManager.GetType`, so it depends on the sanitizer assembly already being loadable by simple name in the host's default load context |
| `System.Web.UI.Design.*`, `System.ComponentModel.Design.*` designer base classes | **C** | 17 files; see below |

### packages.config and assembly references

| Package / reference | Class | Disposition |
| --- | --- | --- |
| `Microsoft.AspNet.Web.Optimization` 1.1.3, `WebGrease` 1.5.2, `Antlr` 3.4.1.9004, `Newtonsoft.Json` 5.0.4 | A | `Rehost.AspNet.Web.Optimization` already carries `Antlr`, `WebGrease` and `Newtonsoft.Json` as package references (`src/Rehost.Web.Optimization/Rehost.Web.Optimization.csproj:28-31`). The `<runtime>` WebGrease binding redirect (`Web.config:46-53`) is removed by the default XDT |
| `Microsoft.Web.Infrastructure` 1.0.0 | A (dropped) | Same disposition as both Milestone 1 applications and eShop |
| `HtmlAgilityPack` 1.4.9 | B | Modern HtmlAgilityPack ships `netstandard2.0`; `DefaultHtmlSanitizer.cs` uses only `HtmlDocument`, `HtmlNode`, `HtmlAttribute` and the three `Option*` flags |
| `System.Web`, `System.Web.Extensions` | A | Rehost identity after recompile |
| **`System.Design`**, **`System.Web.Extensions.Design`** | **C / D** | No modern .NET implementation exists and none is planned. Must be severed — see below |
| `System.Drawing` | **split** | `Color`, `Size`, `KnownColor` are `System.Drawing.Primitives` and portable. `ColorTranslator`, `SystemColors`, `ToolboxBitmapAttribute`, `System.Drawing.Design.UITypeEditor` are not — see below |
| `System.Data` (`DataSet`, `DataTable`) | A/B | In-box on net10 |
| `Microsoft.CSharp` (the `dynamic` call in `AjaxFileUploadHelper`) | A | In-box |

### Web.config

| Entry | Class | Note |
| --- | --- | --- |
| `<trust level="Medium"/>` (`:39`) | **C, blocking** | `src/Rehost.Web/Compatibility/Hosting/ApplicationConfigurationPreflight.cs:49-53` throws `PlatformNotSupportedException` unless the level is exactly `Full`. Activation never completes. One-line XDT removal |
| **no `<httpRuntime>` element** | **C, blocking** | map: `<httpRuntime targetFramework="4.5" />` or later is **Required**. `ApplicationConfigurationPublicationTests.An_Application_Declaring_No_Target_Framework_Is_Refused_Per_Request` shows the observable: every request renders a 500 naming `targetFramework` (ledger P40). One-line XDT insertion |
| `<compilation debug="true" targetFramework="4.0"/>` (`:26`) | A (unassessed) | `compilation targetFramework="4.0"` selects 4.0-era `controlRenderingCompatibilityVersion` and `MultiTargetingUtil` branches (`src/System.Web.ReferenceSource/Compilation/MultiTargetingUtil.cs:442`). No port claim covers pre-4.5 rendering compatibility |
| `<machineKey>` with literal SHA1 keys (`:27`) | A | map: view-state protection with an explicit literal `<machineKey>` — Supported |
| `<configSections>` + `<ajaxControlToolkit …>` (`:7-14`) | A | Custom sections work; `requirePermission="false"` is a partial-trust artifact and inert |
| `<location path="Temp"><system.webServer><handlers><clear/>…` (`:15-24`) | **unverified** | The port builds folder handler lists by discovering folder `web.config` files (`src/Rehost.Web/Compatibility/IisConfig/IisFolderHandlers.cs:14-21`), not from `<location>` blocks in the root file. The `<modules><clear/>` half is inert on IIS too (MH24). Hardening only; no demo journey depends on it |
| `<siteMap><providers>` (`:28-32`) | A (unassessed) | Named provider; `SiteMapDataSource` selects it explicitly, so the root default provider is never asked for `Web.sitemap` — **unverified** |
| `<pages><controls>` toolkit + `namespace="InfoBlock"` with no assembly (`:33-38`) | A (unverified) | The assembly-less form resolves against the `App_Code` assembly |
| `<system.webServer><handlers>` `AjaxFileUploadHandler.axd` (`:41-45`) | A | map: handlers Supported. Note `path` has no leading `*`/`/` |
| `<runtime><assemblyBinding>` WebGrease redirect (`:46-53`) | A (dropped) | Default XDT removes `<runtime>` wholesale (`src/Rehost.Web.AspNetCore/build/Web.Rehost.config:7`) |

There is no `<httpModules>` block, no `<sessionState>` element, no connection
string and no database. Nothing in the site talks to SQL.

### Global.asax and PreApplicationStart wiring

`Global.asax:8-23` registers a `~/bundles/MsAjaxJs` script bundle over
`~/Scripts/WebForms/MsAjax/*.js` — **files that do not exist in the site tree**,
and that no page references — then sets `BundleTable.EnableOptimizations = true`
and deletes every subdirectory of `~/Temp`.

The load-bearing wiring is elsewhere.
`AjaxControlToolkit.StaticResources` carries
`[assembly: PreApplicationStartMethod(typeof(PreApplicationStartCode), "Start")]`
(`AjaxControlToolkit.StaticResources/Properties/AssemblyInfo.cs:7`), and
`PreApplicationStartCode.Start()` does three things:

1. When `useStaticResources="true"` — which `Web.config:11` sets — calls
   `ToolkitResourceManager.RegisterScriptMappings(null)`, adding a
   `ScriptResourceMapping` definition per embedded script whose `Path` points at
   physical `~/Scripts/AjaxControlToolkit/Release/*.js`.
2. Registers the `~/Scripts/AjaxControlToolkit/Bundle` script bundle and the
   `~/Content/AjaxControlToolkit/Styles/Bundle` style bundle that
   `Layout.master:10,17` render on every page.
3. Enumerates bundles from an optional `AjaxControlToolkit.config` at the
   application root. **The sample site ships none**, so
   `BundleResolver.GetControlTypesInBundles` falls through to
   `ControlDependencyMap.Maps` and the default bundle covers *every* toolkit
   control (`Bundling/BundleResolver.cs:37-47`).

Consequence worth stating plainly: because `useStaticResources="true"`, the
sample site's shipped configuration mostly *bypasses* `WebResource.axd` and
`ScriptResource.axd` for toolkit assets — scripts, styles and images all resolve
to physical paths — `ToolkitResourceManager.cs:167` and `:269` rather than the
`GetWebResourceUrl` branches at `:169` and `:273`. That materially de-risks the
embedded-resource surface for this application, while leaving it fully exposed
for any consumer using the toolkit's own default (`useStaticResources` defaults
to `false`, `AjaxControlToolkitConfigSection.cs:10`).

`ControlDependencyMap.CreateDependencyMaps` runs inside that
pre-application-start path and reflects over ~140 named control types, calling
`GetCustomAttributes(true)` on each (`ControlDependencyMap.cs:171,191`). That is
the single most dangerous line in the tree for this port — see next.

## Design-time and `System.Drawing`: the severance problem

This is the largest recompile obstacle, and it has no eShop precedent.

**`System.Design` and `System.Web.Extensions.Design` do not exist on modern
.NET.** The toolkit compiles 50 `*Designer*.cs` files into the *runtime*
assembly. Seventeen of them touch designer base types directly —
`ControlDesigner`, `DataBoundControlDesigner`, `ListControlDesigner`,
`CompositeControlDesigner`, `ExtenderControlDesigner`, `DesignerActionList`,
`TemplatedEditableDesignerRegion` — and the rest derive from
`ExtenderBase/Design/ExtenderControlBaseDesigner.cs`, which derives from
`ExtenderControlDesigner`. Two runtime files reach back into that graph:
`HtmlEditor/Editor.cs:588` and `HtmlEditor/Toolbar.cs:145` take a
`DesignerWithMapPath` parameter (declared in `HtmlEditor/EditorDesigner.cs:37`),
and `HtmlEditor/EditPanel.cs:34,418` holds and calls a `ControlDesigner`. So the
sever is "drop 50 files plus fix three signatures", not "drop 50 files".

**Attribute materialisation is the sharp edge.** The port already hit exactly
this and wrote it down:

> `System.Drawing.Common` is Windows-gated, and the real `ToolboxBitmapAttribute`
> reaches GDI+ from its type initializer, which the page parser triggers.
> — `src/Rehost.Web/Compatibility/DesignTime/DrawingMarkers.cs:1-2`

The toolkit carries 51 `[ToolboxBitmap(typeof(ToolboxIcons.Accessor), …)]`
attributes (e.g. `Accordion/Accordion.cs:26`, `Tabs/TabContainer.cs:25`) and 58
files using `System.Drawing.Design` for
`[Editor("System.Web.UI.Design.ImageUrlEditor, System.Design, …",
typeof(UITypeEditor))]` (`Slider/SliderExtender.cs:78`,
`MultiHandleSlider/MultiHandleSliderExtender.cs:451`). Those attributes are
materialised at least three ways in this application:
`ControlDependencyMap.cs:171,191` at pre-application start,
`ExtenderControlBase.CheckIfValid` via `TypeDescriptor.GetProperties` on every
render (`ExtenderBase/ExtenderControlBase.cs:236`), and
`ComponentDescriber.DescribeComponent` (`ExtenderBase/ComponentDescriber.cs:27`).

The port solved this for its own imported source with three internal marker
files — `Compatibility/DesignTime/UITypeEditor.cs`,
`DrawingMarkers.cs`, `DesignerServiceMarkers.cs`. **All three are `internal`**,
so a recompiled `AjaxControlToolkit` cannot reference them and must carry its
own equivalents (or have the attributes stripped). Deciding which is a design
question the port has not faced yet: publishing the markers would be a new
public surface, and stripping them changes the third-party assembly's metadata.

**Genuine runtime `System.Drawing`** is one call site, not the sea of `using`
lines: `ColorTranslator.ToHtml` in `AsyncFileUpload/AsyncFileUpload.cs:528,572,
594,597,600`. `ColorTranslator` lives in `System.Drawing.Common`, which is
Windows-gated on modern .NET; whether this particular pure-managed conversion
throws off Windows is **unverified** and should be measured rather than assumed.
`SystemColors` appears only in designers (`Tabs/TabContainerDesigner.cs:133`,
`ReorderList/ReorderListDesigner.cs:209`), and `System.Drawing.Imaging` only in
`ReorderList/ReorderListDesigner.cs:10` — both severed with the designers.
Everything else is `Color`/`Size` in property types and attribute defaults,
which are `System.Drawing.Primitives` and portable.

## Windows-only, and where the boundary falls

| Item | Reached by | Disposition |
| --- | --- | --- |
| `[DllImport("Kernel32.dll")] CreateHardLink` | `AjaxControlToolkit.LinkStaticResources/Program.cs:104`, run as the toolkit's `PostBuildEvent` | **D**, build-time only. Replace with a copy step. The same file hard-codes `@"AjaxControlToolkit.SampleSite\"`, `@"Scripts\AjaxControlToolkit\"` and siblings (`:19-24`), so it is doubly Windows-bound |
| `System.Design`, `System.Web.Extensions.Design` | toolkit compile references | **D**, no modern implementation; sever |
| `ToolboxBitmapAttribute`, `UITypeEditor`, `SystemColors`, `System.Drawing.Imaging` | attributes and designers | **D**, stub or strip |
| `ColorTranslator.ToHtml` | `AsyncFileUpload` render | **D?**, unverified; single call site, trivially replaceable with a local hex formatter |

A full scan of `AjaxControlToolkit`, `AjaxControlToolkit.SampleSite`,
`AjaxControlToolkit.HtmlEditor.Sanitizer` and
`AjaxControlToolkit.StaticResources` for `DllImport`, `Microsoft.Win32.Registry`,
`RegistryKey`, `EventLog`, `Process.Start`, `Marshal.`, `ComImport`,
`WindowsIdentity` and `ProtectedData` returns **exactly one hit**, the
`CreateHardLink` above. There is no COM, no registry, no DPAPI, no WMI.

There is also **no Access/Jet database**: no `.mdb`, no `AccessDataSource`, no
`SqlDataSource`, no connection string. Sample data is `App_Data/CarsService.xml`
and `App_Data/TodoItems.xml` read through `XmlDocument` and `DataSet`. The
Northwind-style hazard the brief anticipated is simply absent from this tree.

`Gravatar/Gravatar.cs:130` uses `MD5CryptoServiceProvider`, which exists and
works cross-platform on modern .NET. `Twitter/TwitterAPI.cs:141` makes an
outbound `HttpWebRequest` to a long-dead API — a network dependency, not a
platform one.

## Surfaces the port has likely never exercised

1. **`ExtenderControl` / `IScriptControl` registration.** Every one of ~40
   extenders flows through `ExtenderControlBase.GetScriptDescriptors` /
   `GetScriptReferences` → `ScriptManager.RegisterExtenderControl` →
   `ScriptBehaviorDescriptor` serialisation into the `Sys.Application` init
   block. The types are all imported; the map's Supported row covers ScriptManager
   and UpdatePanel, and [extensions-compatibility](../extensions-compatibility.md)
   names no extender scenario. This is the toolkit's spine.
2. **`ScriptResource.axd` for a third-party assembly at scale.** 538 declared web
   resources across scripts, minified scripts, CSS, minified CSS, images and 36
   localisation scripts. The port's evidence covers 13 scripts it generates and
   embeds itself, plus one image over `WebResource.axd`. The sample's
   `useStaticResources="true"` mostly routes around this; the toolkit's default
   does not.
3. **Production Optimization.** `Global.asax:15` sets
   `BundleTable.EnableOptimizations = true`, so both bundles are combined and
   minified through WebGrease on first render — roughly 90 scripts and the style
   set. The backlog says exactly this is unvalidated: "Validate production
   Optimization combination, caching, request handling, and minification
   separately from the frozen template's debug-mode expansion." Both Milestone 1
   templates and eShop run bundles in **debug** mode.
4. **Client callbacks.** `WebForm_DoCallback` / `__CALLBACKID` round trips from
   `Rating` and `ScriptControlBase`. Present in `src/`, no claim.
5. **Control designers.** Design-time only and severable — the good news of this
   section. Nothing at runtime needs them once the three `HtmlEditor` signatures
   are fixed.
6. **Web Site publish.** `CodeFile`, `App_Code`, and inline `Global.asax` must
   ship as source in the published tree; the open follow-up owns it.

## Cross-platform hazards in the frozen application

These are authored properties of the tree, not port gaps, and would surface as
Linux/macOS-only failures.

| Hazard | Evidence | Effect |
| --- | --- | --- |
| Static resources are Windows-hard-linked build output | `LinkStaticResources/Program.cs:19-24,104` | The site cannot render at all without `Scripts/AjaxControlToolkit/{Release,Debug}` and `Content/AjaxControlToolkit/{Styles,Images}`. Must be produced by a portable copy step before import |
| `Global.asax` bundle over non-existent `~/Scripts/WebForms/MsAjax/*.js` | `Global.asax:9-13` | Dead registration; nothing renders `~/bundles/MsAjaxJs`. Harmless but confusing |
| Case sensitivity across 102 `App_Data/ControlReference/*.html` reads | `Layout.master.cs:23,31` builds the filename from a markup attribute | Every page render does a literal `File.ReadAllText`; a single casing mismatch is a hard failure on a case-sensitive filesystem. Not audited here — **unverified** |
| `Path.GetTempPath()` fallback for uploads | `AjaxFileUpload/AjaxFileUpload.cs:323` | Unreached: `Web.config:14` sets `tempFolder="~/Temp"`, which goes through `Server.MapPath` (`:328`) |

A scan for backslash path literals across the toolkit, the sanitizer, the
static-resources assembly and the sample site returns **no hits** outside
`LinkStaticResources`. `Path.Combine` is used correctly everywhere else.

## Verdict

**Expected to recompile cleanly.** `AjaxControlToolkit.HtmlEditor.Sanitizer` is
five small files over HtmlAgilityPack and `System.Web`; a modern
HtmlAgilityPack plus Rehost identity should be enough.
`AjaxControlToolkit.StaticResources` is one file over
`System.Web.Optimization`, which the port already ships.
`AjaxControlToolkit` itself compiles cleanly **minus the design-time closure**.
The sample site's own code — 6 `App_Code` files, 2 masters, 51 mostly-declarative
pages, 3 `.asmx` — reaches nothing exotic.

**Two one-line configuration blockers, both known and both cheap.**
`<trust level="Medium"/>` refuses activation outright, and the missing
`<httpRuntime targetFramework>` 500s every request. Both are XDT edits of exactly
the eShop kind. They are listed first below only because nothing runs until they
are gone.

**Windows-only, needing substitution.** `System.Design` /
`System.Web.Extensions.Design` (sever the 50 designer files and three
`HtmlEditor` signatures), the `ToolboxBitmap`/`UITypeEditor` attribute closure
(the port's own internal markers prove the mechanism but cannot be reused as-is),
one `ColorTranslator.ToHtml` call site, and the `CreateHardLink` build tool.

### Gap list, ordered by likelihood of blocking a representative demo-page journey

1. **`<trust level="Medium"/>`** (`Web.config:39`) — activation throws
   `PlatformNotSupportedException` before the first request
   (`ApplicationConfigurationPreflight.cs:49-53`). XDT removal.
2. **No `<httpRuntime targetFramework>`** — every request renders a 500 naming
   the setting (ledger P40; `ApplicationConfigurationPublicationTests`). XDT
   insertion of `targetFramework="4.5"` or later.
3. **The design-time closure fails the recompile outright** — `System.Design`
   and `System.Web.Extensions.Design` have no modern implementation, so
   `AjaxControlToolkit.dll` does not build until 50 files are severed and
   `Editor.cs:588`, `Toolbar.cs:145`, `EditPanel.cs:34,418` are adjusted.
4. **`ToolboxBitmap` / `UITypeEditor` attribute materialisation at
   pre-application start** — `ControlDependencyMap.cs:171,191` reflects
   attributes over ~140 control types before the first request. If the real
   `System.Drawing.Common` attribute survives the recompile, startup fails off
   Windows. The port's `DrawingMarkers.cs` is the recipe; its `internal`
   visibility is the open decision.
5. **Static resources do not exist until a portable replacement for
   `LinkStaticResources` produces them** — `Layout.master:10,17` is on every
   page, and with `useStaticResources="true"` every script, style and image
   resolves to a physical path.
6. **Production Optimization** — `EnableOptimizations = true` puts ~90 scripts
   and the CSS set through WebGrease combination and minification on first
   render, the exact path the backlog records as unvalidated. First render of
   any page depends on it.
7. **`ExtenderControl` registration and descriptor emission** — unassessed, and
   every single demo page depends on it.
8. **`ScriptResource.axd` / `WebResource.axd` breadth for a third-party
   assembly** — mostly bypassed by this site's `useStaticResources="true"`, but
   still reached for `System.Web`'s own runtime scripts and for any
   `renderStyleLinks` path.
9. **Web Site project packaging** — `CodeFile`/`App_Code`/inline `Global.asax`
   publish mode is an open follow-up, and this is the port's first real Web Site
   consumer.
10. **`compilation targetFramework="4.0"` rendering compatibility** — selects
    pre-4.5 control rendering branches that carry no port claim. Cosmetic risk,
    listed last.
11. **`ColorTranslator.ToHtml` off Windows** — one control (`AsyncFileUpload`),
    one page. Unverified; trivially substitutable.

### Effort comparison against the eShop bring-up

eShop cost: 2 XDT module drops, 1 library recompile
(`Autofac.Integration.Web`), 1 baseline-config fix (expression builders), 1
script-mapping sidecar.

This application's shape is different in kind, not just in size. The
configuration side is *cheaper* than eShop's — 2 XDT edits, no modules to drop,
no telemetry stack, no database, no binding redirects that the default transform
does not already remove. But the library side is **substantially heavier**:

| Axis | eShop | SampleSite |
| --- | --- | --- |
| XDT edits | 2 module drops | 2 (`<trust>`, `<httpRuntime>`) |
| Library recompiles | 1 (`Autofac.Integration.Web`, ~10 files) | 3 (`AjaxControlToolkit` ~350 files, `.HtmlEditor.Sanitizer`, `.StaticResources`) |
| Source surgery inside a recompiled library | none | sever 50 designer files, fix 3 signatures, decide the attribute-marker policy |
| Build-tooling replacement | none | replace the `CreateHardLink` post-build tool |
| New port surface to assess | routing, model binding | extender registration, third-party embedded resources, production Optimization, Web Site publish |
| Windows-only boundary | LocalDb + AI perf counters, both unreached in the journey | design-time closure, reached at startup |

A fair estimate is **two to three times the eShop effort**, with most of it in
one place: getting `AjaxControlToolkit.dll` to compile and start without the
design-time closure. The XDT and configuration work is a fraction of eShop's.
The unknowns that could move the estimate are the Web Site publish mode (an open
follow-up with no scoping yet) and production Optimization behaviour, neither of
which has been measured.

## Sources

- Application and library: `AjaxControlToolkit` at `0769b45`
  (`AjaxControlToolkit.sln`, `AjaxControlToolkit.SampleSite/{Web.config,
  Global.asax, Layout.master*, Samples.master*, Default.aspx*, packages.config,
  App_Code/*, App_Data/*, Samples.sitemap}`,
  `AjaxControlToolkit/{AjaxControlToolkit.csproj, ToolkitConfig.cs,
  AjaxControlToolkitConfigSection.cs, ToolkitResourceManager.cs,
  ControlDependencyMap.cs, Localization.cs, Properties/AssemblyInfo.cs,
  ExtenderBase/**, Bundling/**, AjaxFileUpload/**, AsyncFileUpload/**,
  HtmlEditor/**, */*Designer*.cs}`,
  `AjaxControlToolkit.HtmlEditor.Sanitizer/**`,
  `AjaxControlToolkit.StaticResources/**`,
  `AjaxControlToolkit.LinkStaticResources/Program.cs`)
- Support claims: [compatibility map](../compatibility.md),
  [Extensions compatibility](../extensions-compatibility.md)
- Unresolved work referenced: [backlog](../backlog.md),
  [project models](../follow-ups/web-site-vs-wap-project-models.md),
  [resource timestamps](../follow-ups/web-resource-assembly-timestamps.md)
- Precedents: [eShop portability](eshoplegacywebforms-portability.md),
  [Identity application findings](webforms-identity-application-gaps.md)
