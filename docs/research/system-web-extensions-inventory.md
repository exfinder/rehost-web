# `System.Web.Extensions` inventory

## Result

The pinned Microsoft Reference Source is a broad implementation baseline, not a
reproducible 4.8.1 build: 271 C# files / 53,089 lines, 70 JavaScript fragments and 13
composition files / 11,818 lines, but no project or complete embedded-resource inputs
([source tree](../../src/System.Web.Extensions.ReferenceSource/)). The shipped assembly is
managed MSIL. Its only two P/Invokes are `wininet.dll` cookie calls in Client Services
([source](../../src/System.Web.Extensions.ReferenceSource/ClientServices/Providers/ProxyHelper.cs#L443-L452)).

The frozen template reaches only ScriptManager, script mappings/references, lifecycle and
resource/path rendering, and two package-registered bundles. Partial postbacks, AJAX services,
WCF proxy compilation, Client Services and data controls were outside that initial slice.
Current support is recorded in the [compatibility map](../compatibility.md); remaining scope is
in the [Extensions follow-up](../follow-ups/extensions-ajax-activation.md).

## Scale and composition

Source is pinned at `ec9fa9ae770d522a5b5f0607898044b7478574a3`; the local license is
[MIT](../../third_party/microsoft/referencesource/LICENSE.txt). A read-only metadata inspection
of Windows x64 .NET Framework 4.8.1 file version `4.8.9319.0` found an IL-only, 1,846,224-byte
assembly with 164 exported types and 1,874 declared public members. No proprietary method body
was decompiled.

| Area | Exported types | C# source | Responsibility |
|---|---:|---:|---|
| `System.Web.UI` | 45 | 77 files / 11,192 lines | ScriptManager, partial rendering, Timer, descriptors |
| WebControls + expressions | 66 | 80 / 15,164 | ListView, DataPager, LINQ/context sources, QueryExtender |
| Script serialization/services | 11 | 28 / 4,352 | JSON, ASMX/page-method proxies and REST handlers |
| Application Services | 10 | 11 / 1,220 | Authentication, profile and role endpoints |
| Client Services | 11 | 14 / 3,810 | Desktop/offline providers, settings and cookies |
| Configuration | 10 | 10 / 692 | `system.web.extensions` sections |
| Handlers | 2 | 3 / 1,031 | `ScriptResource.axd`, `ScriptModule` |
| WCF compilation | 2 | 30 / 10,064 | Service/data-service proxy generation |
| Dynamic Data/query | 6 | 4 direct files plus UI code | Dynamic query/data-source contracts |
| Resources/other internals | 0 | 14 / 5,483 | Resource designers, wrappers, utilities |

Client Services is 7% of C# lines and contains all direct Windows coupling, but the other 93%
is not 93% mechanical: WCF, Data Services, LINQ-to-SQL, design-time, CAS and private
`System.Web` contracts add substantial porting work.

## Build closure and dependencies

The source lacks a `.csproj` and authoritative compile/resource list. It contains five `.resx`
files and nine bitmaps; the shipped manifest contains six resource sets, nine bitmaps, 26 final
debug/release scripts and two WCF XSDs. `AssemblyInfo` says scripts were linked from external
`AtlasBuildOutput`
([declarations](../../src/System.Web.Extensions.ReferenceSource/Properties/AssemblyInfo.cs#L38-L101)).
Shared build constants are also absent. A port therefore needs explicit asset generation/copy
rules; copying C# cannot reproduce ScriptManager behavior. Framework readings arbitrate any
difference between this snapshot and serviced 4.8.1.

The binary directly references 19 assemblies. Foundation dependencies are `mscorlib`,
`System`, `System.Core`, `System.Configuration`, `System.Xml` and `System.Data`; Web Forms adds
`System.Web`, `System.Web.ApplicationServices` and `System.Web.Services`. Whole-assembly
obstacles are WCF/service-generation assemblies, `System.Data.Linq`, `System.Data.Entity`,
`System.Drawing`, `System.Design` and `System.Windows.Forms`. These must not become accidental
portable runtime dependencies.

## `System.Web` and configuration coupling

`System.Web` grants the Framework assembly friend access
([declaration](../../src/System.Web.ReferenceSource/Properties/AssemblyInfo.cs#L20-L24)).
Behavior-critical uses include:

- internal ScriptManager/UpdatePanel protocols and Page item registration
  ([registration](../../src/System.Web.Extensions.ReferenceSource/ui/ScriptManager.cs#L1251-L1287));
- shared script-resource mapping
  ([mapping](../../src/System.Web.Extensions.ReferenceSource/ui/ScriptManager.cs#L116-L118));
- partial-render interception and response writer switching
  ([rendering](../../src/System.Web.Extensions.ReferenceSource/ui/PageRequestManager.cs#L664-L767));
- protected resource URLs using Page crypto, `Purpose` and resource internals
  ([handler](../../src/System.Web.Extensions.ReferenceSource/Handlers/ScriptResourceHandler.cs#L274-L325));
- BuildManager/runtime assembly discovery
  ([selection](../../src/System.Web.Extensions.ReferenceSource/ui/ScriptManager.cs#L312-L342)).

The approved sibling `Rehost.Web.Extensions` assembly uses direct Runtime friend access,
preserving the seam without public adapters or Framework identity claims.

Framework configuration also registers the section group, compilation assembly, handlers,
`ScriptModule-4.0`, and page-control namespaces
([sections](../../third_party/microsoft/framework-config/machine.config#L126),
[handlers/modules](../../third_party/microsoft/framework-config/web.config#L173),
[controls](../../third_party/microsoft/framework-config/web.config#L348)). Packaging the DLL
without deterministic registration leaves features inactive.

## Platform hotspots

All direct Windows coupling is in Client Services: two WinINet cookie imports, Windows identity,
WinForms user-data paths, and OleDb/SQL CE offline storage. Design-time Drawing/Design references
should be omitted or isolated. WCF activation/code generation, legacy Data Services and
LINQ-to-SQL require replacement, exclusion or separate packages. `WCFBuildProvider` also assumes
CodeDom and BuildManager compilation
([source](../../src/System.Web.Extensions.ReferenceSource/Compilation/WCFBuildProvider.cs#L100-L220)).
No registry or COM import was found. Full-trust project policy permits removal of CAS behavior.

`ScriptResourceHandler` branches on GAC identity and embeds assembly identity in protected URLs
([encoding](../../src/System.Web.Extensions.ReferenceSource/Handlers/ScriptResourceHandler.cs#L672-L706));
portable resolution and app-local deployment therefore require explicit semantics.

## Template scripts and package helpers

The template uses `<asp:ScriptManager>`, `MsAjaxBundle`, `jquery`, `WebFormsBundle`, and
`ScriptResourceMapping.AddDefinition`
([master](../../apps/WebFormsApplication/WebFormsApplication/Site.Master#L21-L38),
[registration](../../apps/WebFormsApplication/WebFormsApplication/App_Start/BundleConfig.cs#L40-L51)).

`Scripts/WebForms/MSAjax/*.js` are 11 release Microsoft AJAX files from
`Microsoft.AspNet.ScriptManager.MSAjax`; `Scripts/WebForms/*.js` are System.Web control scripts
from `Microsoft.AspNet.ScriptManager.WebForms`. The application commits them because restoring
`packages.config` does not reliably recreate deleted content. Runtime embeds every declared
System.Web resource; generated Microsoft AJAX/debug/localized resources remain separate scope.

The two package DLLs are startup registration helpers, not ScriptManager implementations. Their
source/license are separate from Reference Source; MSAjax registers `MsAjaxBundle` and 11 script
mappings, WebForms registers `WebFormsBundle`
([MSAjax](https://www.nuget.org/packages/Microsoft.AspNet.ScriptManager.MSAjax/5.0.0),
[WebForms](https://www.nuget.org/packages/Microsoft.AspNet.ScriptManager.WebForms/5.0.0)).
Replacement registration reproduces those names. Extensions discovers Optimization through
BuildManager/reflection, avoiding a direct assembly dependency
([adapter](../../src/System.Web.Extensions.ReferenceSource/ui/BundleReflectionHelper.cs#L12-L35)).

The imported [Optimization source](../../src/System.Web.Optimization.ReferenceSource/) has no
native imports; its legacy managed dependencies include WebGrease, Antlr and Newtonsoft.Json.
A macOS/.NET 10 probe ran WebGrease 1.6.0 JS/CSS minification successfully, with expected NU1701
warnings; image processing was not established.

## Porting character

| Area | Assessment |
|---|---|
| ScriptManager full-page resources | Moderate: lifecycle, identity, ordering and assets |
| UpdatePanel/async postback | Hard: Page/render interception and wire protocol |
| `ScriptResource.axd` | Hard: crypto URL, localization, compression, caching |
| ListView/DataPager/query | Moderate–hard: broad controls and legacy data APIs |
| AJAX/application services | Hard: configuration, serialization and security interaction |
| WCF build provider | Very hard, low current value |
| Client Services | Non-portable as written |

Detailed compile boundaries and staged cuts live in the
[portability analysis](system-web-extensions-portability.md).
