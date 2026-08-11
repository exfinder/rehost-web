# `System.Web.Extensions` inventory

## Result

Porting the whole assembly is possible in principle, but not mostly a mechanical
copy. Microsoft Reference Source contains the broad managed implementation:
271 C# files / 53,089 lines plus 70 JavaScript files and 13 composition files /
11,818 lines. It does not contain a build project or every input embedded in the
shipped assembly. The pinned source is therefore an implementation baseline,
not a reproducible 4.8.1 source package
([source tree](https://github.com/microsoft/referencesource/tree/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions)).

The assembly is managed MSIL. Metadata shows no native method bodies and two
P/Invoke declarations, both `wininet.dll` cookie APIs in Client Services
([source](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/ClientServices/Providers/ProxyHelper.cs#L443-L452)).
By implementation format it is effectively **100% managed / 0% native**, with
two managed declarations entering Win32. A conservative source-area classifier
is **about 93% outside directly Windows-coupled Client Services / 7% inside**:
that subtree is 3,810 of 53,089 C# lines and also uses Windows identity, WinForms
user-data paths, and OleDb. This is not an effort estimate. Framework-only WCF,
Data Services, LINQ-to-SQL, design-time, CAS, and `System.Web` internal contracts
make substantially more than 7% non-mechanical.

The frozen template reaches a much smaller vertical slice: `ScriptManager`,
script references/mapping, resource/path rendering, lifecycle integration, and
the two package-registered bundle names. It does not reach partial postbacks,
AJAX web services, WCF proxy compilation, Client Services, or the data controls.

## Evidence and scale

Source measurements use the sibling official clone at revision
`ec9fa9ae770d522a5b5f0607898044b7478574a3`; line counts include comments and
blank lines. The repository says Reference Source files are MIT unless a file
states otherwise
([README](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/README.md#license),
[license](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/LICENSE.txt)).

The API inventory is a read-only reflection/PE-metadata reading of Windows x64
.NET Framework 4.8.1 file version `4.8.9319.0`, assembly identity
`System.Web.Extensions, Version=4.0.0.0`, size 1,846,224 bytes. No proprietary
method body was decompiled. Its PE flags are `ILOnly`, I386/AnyCPU-style. It has
**164 exported types** and **1,874 declared public members** (reflection counts
accessors and constructors).

| Area | Exported types | C# source | Main responsibility |
| --- | ---: | ---: | --- |
| `System.Web.UI` | 45 | 77 files / 11,192 lines | ScriptManager, references/mappings, partial rendering, UpdatePanel, Timer, descriptors, extenders |
| `UI.WebControls` + expressions | 66 | 80 files / 15,164 lines | ListView, DataPager, LINQ/context data sources, QueryExtender |
| Script serialization/services | 11 | 28 files / 4,352 lines | JSON serializer, ASMX/page-method proxies and REST handlers |
| Application Services | 10 | 11 files / 1,220 lines | AJAX authentication, profile, and role service endpoints |
| Client Services | 11 | 14 files / 3,810 lines | Desktop/offline membership, roles, settings, cookies |
| Configuration | 10 | 10 files / 692 lines | `system.web.extensions` sections |
| Handlers | 2 | 3 files / 1,031 lines | `ScriptResource.axd`, `ScriptModule` |
| WCF compilation | 2 | 30 files / 10,064 lines | `WCFBuildProvider`, service/data-service proxy generation |
| Dynamic Data/query support | 6 | 4 direct files plus UI data code | Dynamic data-source contracts and dynamic query parser |
| Management | 1 | 1 file / 22 lines | web-service error event |
| Generated resources and other internals | 0 | 13 files / 5,461 lines | resource designers, wrappers, globalization, utilities |

Largest exported namespaces: `System.Web.UI.WebControls` 51,
`System.Web.UI` 45, expressions 15, Application Services 10, configuration 10,
Client Services providers 8. The assembly is several historically bundled
products, not one ScriptManager module.

## Source completeness and assets

The source tree has the principal implementation bodies and public types, but
not a complete build closure:

- no `.csproj` or authoritative compile/resource list;
- 5 `.resx` files and 9 toolbox bitmaps, but the shipped assembly also embeds a
  sixth resource set (`WCFModelStrings`) and two WCF schema XSDs not present as
  source files;
- 70 JavaScript fragments and 13 `.jsa` composition manifests, but not the 26
  final debug/release `MicrosoftAjax*`/calendar resources embedded by Framework;
  `AssemblyInfo` says these were linked from an external `AtlasBuildOutput`
  ([resource declarations](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/Properties/AssemblyInfo.cs#L38-L101));
- generated resource designers and WCF XML serializers are present, while
  shared build constants such as `AssemblyRef` are not in this directory;
- the published snapshot is not guaranteed identical to the serviced 4.8.1
  binary. The shipped binary has 395 total metadata types, but private and
  compiler-generated type counts do not establish source equivalence; Framework
  readings still arbitrate differences.

The shipped manifest has 43 resources: 6 resource sets, 9 bitmaps, 26 scripts,
and 2 XSDs. A port needs an explicit, reproducible asset-generation/copy policy;
copying C# alone cannot reproduce ScriptManager behavior.

## Assembly dependencies

The 4.8.1 assembly directly references 19 assemblies:

- foundation: `mscorlib`, `System`, `System.Core`, `System.Configuration`,
  `System.Xml`, `System.Data`;
- Web Forms: `System.Web`, `System.Web.ApplicationServices`,
  `System.Web.Services`;
- WCF/service generation: `System.ServiceModel`,
  `System.ServiceModel.Activation`, `System.Runtime.Serialization`,
  `System.Data.Services.Client`, `System.Data.Services.Design`;
- data controls: `System.Data.Linq`, `System.Data.Entity`;
- desktop/design: `System.Drawing`, `System.Design`, `System.Windows.Forms`.

The last three groups are the whole-assembly obstacle. Several have no direct
modern .NET equivalent, are design-time only, or are incompatible legacy stacks.
They should not become accidental transitive runtime requirements. CAS and
partial-trust assertions are also pervasive; this project's full-trust process
contract permits removing/replacing them, but that is source adaptation.

## `System.Web` coupling

`System.Web` deliberately grants `System.Web.Extensions` friend access
([friend declaration](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web/Properties/AssemblyInfo.cs#L20-L24)).
The source uses that privilege in behavior-critical paths:

- `IScriptManager`, `IScriptResourceDefinition`, `IScriptResourceMapping`, and
  `IUpdatePanel` are internal cross-assembly protocols. `ScriptManager` installs
  itself in the Page items slot during `OnInit`
  ([registration](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/ui/ScriptManager.cs#L1251-L1287));
- `ClientScriptManager._scriptResourceMapping` is populated by ScriptManager and
  used by System.Web resource/script registration
  ([mapping](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/ui/ScriptManager.cs#L116-L118));
- partial rendering needs internal Page/form render delegation, hidden-field
  inventory, and `HttpResponse.SwitchWriter`
  ([render interception](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/ui/PageRequestManager.cs#L664-L767));
- script-resource URLs use internal `Page.EncryptString`/`DecryptString`,
  cryptographic `Purpose`, `AssemblyResourceLoader`, `AppSettings`, and resource
  helpers
  ([handler path](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/Handlers/ScriptResourceHandler.cs#L274-L325));
- assembly/framework selection uses internal `RuntimeConfig` plus BuildManager
  and hosting assembly discovery
  ([selection](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/ui/ScriptManager.cs#L312-L342)).

Most of these internals remain in the imported runtime source, but the existing
strong-named friend declaration does not grant access to a new
`Rehost.WebForms.*` assembly. The approved design uses
`Rehost.WebForms.Extensions` plus direct Runtime friend access. This preserves
the original sibling-assembly relationship without publishing adapter APIs or
claiming Framework binary identity.

Framework configuration is also part of the assembly boundary. It declares the
`system.web.extensions` section group, registers `ScriptResource.axd`, routes
ASMX/application-service requests through `ScriptHandlerFactory`, installs
`ScriptModule-4.0`, adds the assembly to dynamic compilation, and registers its
three page-control namespaces
([machine sections](../../third_party/microsoft/framework-config/machine.config#L126),
[handlers/modules](../../third_party/microsoft/framework-config/web.config#L173),
[page controls](../../third_party/microsoft/framework-config/web.config#L348)).
The portable root configuration currently omits that closure. Packaging the DLL
without deterministic configuration publication would leave major features
inactive.

## Platform hotspots

- **Native:** exactly two `wininet.dll` imports, both IE-cookie integration in
  Client Services. No native method bodies.
- **Windows desktop:** Client Services uses `WindowsIdentity`/
  `WindowsPrincipal`, `System.Windows.Forms.Application.UserAppDataPath`, OleDb,
  and isolated-storage/file conventions
  ([offline marker](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/ClientServices/ConnectivityStatus.cs#L30-L51)).
- **Design-time:** Drawing/Design references mostly toolbox attributes, bitmap
  icons, and editors. These should be isolated or omitted from portable runtime
  behavior, not pull WindowsDesktop into it.
- **Framework-only services/data:** WCF activation/proxy generation, legacy Data
  Services design, LINQ-to-SQL, and `System.Data.Entity` need replacement,
  deliberate exclusion, or separate packages. `WCFBuildProvider` also assumes
  CodeDom and BuildManager compilation
  ([source](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/Compilation/WCFBuildProvider.cs#L100-L220)).
- **Deployment/identity:** `ScriptResourceHandler` branches on GAC identity and
  emits assembly identity into protected URLs
  ([encoding](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/Handlers/ScriptResourceHandler.cs#L672-L706)).
  Portable assembly resolution and app-local deployment need defined semantics.
- **IIS/config:** `TrySkipIisCustomErrors` is an IIS compatibility hint; the
  module/handler/config-section behavior itself is managed. No registry or COM
  import was found; the assembly declares `ComVisible(false)`.

## Stock application and ScriptManager packages

The application directly reaches:

- `<asp:ScriptManager>` with `ScriptReference` children and named
  `MsAjaxBundle`, `jquery`, and `WebFormsBundle`
  ([master](../../apps/WebFormsApplication/WebFormsApplication/Site.Master#L21-L38));
- `ScriptManager.ScriptResourceMapping.AddDefinition` and
  `ScriptResourceDefinition` for jQuery
  ([registration](../../apps/WebFormsApplication/WebFormsApplication/App_Start/BundleConfig.cs#L40-L51));
- the existing runtime's Page lifecycle and client-script registration, plus
  physical application-local JS files.

The physical files split into two ownership families:

- `Scripts/WebForms/MSAjax/*.js` are the 11 release Microsoft AJAX files
  installed by `Microsoft.AspNet.ScriptManager.MSAjax`. They correspond to most
  release-script resources normally embedded in `System.Web.Extensions`; the
  Framework assembly also carries 11 debug variants and four release/debug
  calendar resources.
- `Scripts/WebForms/*.js` (`WebForms.js`, validation, menu/tree/data controls,
  and related files) belong to `System.Web` and were installed by
  `Microsoft.AspNet.ScriptManager.WebForms`. Their sources exist under
  `src/System.Web.ReferenceSource/UI/WebControls/RuntimeScripts`. Runtime now
  embeds every resource it declares, scripts and control images alike.

The legacy NuGet install copied these files from package content; it did not
extract them from Framework assemblies. A clean `packages.config` restore does
not reliably recreate deleted project content. The frozen application already
commits the files and gives each relevant `ScriptReference` a physical path or
named bundle, so neither assembly's embedded-resource path is required for the
first visual slice.

`Microsoft.AspNet.ScriptManager.MSAjax` 5.0.0 and
`Microsoft.AspNet.ScriptManager.WebForms` 5.0.0 are not ScriptManager itself.
Each package contains one small managed assembly with one public
`PreApplicationStartCode` type plus content JS. Each DLL references
only `mscorlib`, `System`, `System.Web`, and
`System.Web.Extensions, Version=4.0.0.0`; their NuSpecs describe automatic
registration of optimization bundles
([MSAjax package](https://www.nuget.org/packages/Microsoft.AspNet.ScriptManager.MSAjax/5.0.0),
[WebForms package](https://www.nuget.org/packages/Microsoft.AspNet.ScriptManager.WebForms/5.0.0)).
Their source is not in Reference Source, and their package license is separate
from Reference Source MIT. Their method bodies were not decompiled.

`System.Web.Extensions` itself avoids a direct Optimization assembly reference:
it discovers `System.Web.Optimization.BundleResolver.Current` and three methods
through BuildManager/reflection
([adapter](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/ui/BundleReflectionHelper.cs#L12-L35),
[discovery](https://github.com/microsoft/referencesource/blob/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions/ui/BundleReflectionHelper.cs#L111-L136)).

Consequences:

- a differently named port does not automatically satisfy those binaries'
  original assembly reference;
- replacement startup registration or an explicit assembly-identity/alias plan
  is required;
- the template's JS content is already committed, but its bundle names still
  depend on startup mapping and later `System.Web.Optimization` support;
- the minimum visual-template slice does not require UpdatePanel or the rest of
  the 164-type assembly to prove ordinary full-page rendering.

## Comparable port evidence

WebFormsForCore uses broad Reference Source imports but does not port the whole
historical dependency graph. Its modern `System.Web.Extensions` project uses
CoreWCF packages for reached service behavior while its WCF build provider
explicitly rejects `.datasvcmap` and `.svcmap` generation
([project](https://github.com/webformsforcore/WebFormsForCore/blob/main/src/WebFormsForCore.Web.Extensions/WebFormsForCore.Web.Extensions.csproj),
[build provider](https://github.com/webformsforcore/WebFormsForCore/blob/main/src/WebFormsForCore.Web.Extensions/Compilation/WCFBuildProvider.cs)).
`LinqDataSource` is compiled only for `NETFRAMEWORK`
([source](https://github.com/webformsforcore/WebFormsForCore/blob/main/src/WebFormsForCore.Web.Extensions/ui/WebControls/LinqDataSource.cs)).
Its documentation excludes visual designers and describes its `System.Drawing`
package as an attribute-level compatibility layer
([documentation](https://webformsforcore.github.io/)). This supports an explicit
compiled feature boundary rather than dragging WCF, LINQ-to-SQL, Data Services,
WinForms, and design stacks into ScriptManager work.

## Optimization and WebGrease findings

ASP.NET Web Optimization is not Reference Source. Its official archived source
is in [`aspnet/AspNetWebOptimization`](https://github.com/aspnet/AspNetWebOptimization);
the pinned sibling revision and planned import trees are recorded in
[`../provenance/aspnet-web-optimization.md`](../provenance/aspnet-web-optimization.md).
No native imports were found. The main obstacles are managed Framework-era
dependencies: `System.Web`, `Microsoft.Web.Infrastructure`, configuration,
WebGrease, Newtonsoft.Json, and Antlr. The separate WebForms project contains
only `BundleReference` plus assembly metadata/config transformation.

An isolated macOS/.NET 10 probe consumed WebGrease 1.6.0 unchanged with
Newtonsoft.Json 13.0.3 and Antlr 3.5.0.2. Both JavaScript and CSS minification
completed with no reported errors. NuGet emits `NU1701` because WebGrease and
Antlr expose only Framework assets. WebGrease's image assembly paths reference
legacy drawing/desktop types; the probe establishes only JS/CSS execution.

## Approved first slice

The unchanged frozen application is the boundary. Import the complete available
`System.Web.Extensions` tree, but compile only the transitive closure required
for full-page `ScriptManager`, `ScriptReference`, script mappings, physical
paths, ordering/deduplication, debug selection, and Optimization discovery.
Excluded public types are absent rather than stubbed, so recompiled consumers
using them fail during compilation. Precompiled Framework binaries remain
outside the compatibility claim.

Use Rehost assembly/package identity, direct Runtime friend access, the physical
files already in the application, and one
`Rehost.WebForms.ScriptManager.Bundles` package for `MsAjaxBundle` and
`WebFormsBundle` startup mappings. Attempt the complete Optimization source
compile, but claim only reached behavior. `ScriptResource.axd`, generated and
embedded Microsoft AJAX resources, UpdatePanel/async postbacks, service/JSON
paths, data controls, and the unrelated legacy stacks remain explicit backlog.
The implementation and evidence sequence is in the
[stock template script-stack plan](../follow-ups/stock-template-script-stack.md).

## Porting assessment

| Area | Likely character |
| --- | --- |
| Enums, attributes, collections, descriptors, JSON primitives | Mostly mechanical after dependency substitutions |
| ScriptManager full-page scripts/mappings/resources | Moderate; lifecycle, resource identity, ordering, localization, debug/CDN and asset packaging must match |
| UpdatePanel/async postback | Hard; deep Page/Response render interception plus a client/server wire protocol |
| `ScriptResource.axd` | Hard; protected URL format, crypto purpose, compression, localization, caching, resource identity |
| ListView/DataPager/query expressions | Moderate to hard; broad WebControls behavior and legacy data abstractions |
| AJAX services/application services | Hard; ASMX/WCF/configuration/serialization/security interaction |
| WCF build provider | Very hard and low current value; obsolete service-generation/tooling stack |
| Client Services | Non-portable as written; isolate, redesign, or explicitly exclude |

The approved plan starts with the reached ScriptManager vertical slice. The full
inventory remains future scope in the backlog rather than becoming accidental
dependencies or silently disappearing.
