# `System.Web.Extensions` portability analysis

Analysis of the imported reference source
(`src/System.Web.Extensions.ReferenceSource`, pinned
[ec9fa9ae](https://github.com/microsoft/referencesource/tree/ec9fa9ae770d522a5b5f0607898044b7478574a3/System.Web.Extensions),
271 C# files / 53,089 lines, plus the script sources covered by
[the inventory](system-web-extensions-inventory.md)) answering: what does the
rest of the assembly need beyond the 160-file closure
`Rehost.WebForms.Extensions` compiles today, which Framework-only stacks does
it touch, and where the clean exclusion lines run. Weighted toward what
template-era Web Forms applications actually used. Companion to the
[inventory](system-web-extensions-inventory.md), which measured the assembly;
this measures the *port distance*.

## Result

**All 271 files compile on .NET 10 against `Rehost.WebForms.Runtime` +
`Rehost.WebForms.WebServices` + `Rehost.WebForms.ApplicationServices` given
the `WEB_EXTENSIONS_CODE` define, four added packages, one duplicate-type file
exclusion, and a shim file whose every symbol is a proven-missing API**
(experiment below). The missing APIs are confined to **16 files in three
legacy stacks** — WCF hosting/codegen, LINQ to SQL, and desktop Client
Services — none of which any AJAX-runtime file touches.

The headline for scope: the features the compatibility table lists as open —
async postbacks/partial rendering, page methods, the JSON application
services, Timer/History — need **zero new API surface**. Their types either
compile today (`UpdatePanel`, `PageRequestManager`, `Timer`,
`ScriptManager` history support are all in the shipped closure) or compile
clean in the experiment (`ScriptModule`, the three internal `[ScriptService]`
application services, 438 lines total). What is missing is the Framework
configuration closure (`ScriptModule-4.0`, `*_AppService.axd`, the
`system.web.extensions` section group), plus evidence.

## Method

Two evidence streams, no repo changes (experiment in the session scratchpad,
`swx-compile/`):

1. **Brute-force compile** — a scratchpad project named
   `Rehost.WebForms.Extensions` (for Runtime friend access) compiling all 271
   files against `net10.0` + the three Rehost assemblies + `System.CodeDom` /
   `System.Configuration.ConfigurationManager` / `System.Security.Permissions`
   (the WebServices set) + `System.ServiceModel.Primitives` 8.1.2 /
   `System.ServiceModel.Http` 8.1.2 / `System.Runtime.Serialization.Schema`
   10.0.10 / `System.Data.OleDb` 10.0.10, with the repo's `System.Web`
   facade-removal target. Iterated to **0 errors** by shimming; every shimmed
   symbol is *proven-missing*, everything else *proven-present*. (Caveat: csc
   reports declaration-phase errors before binding method bodies, so early
   waves understate gaps — the green build is the meaningful endpoint.)
2. **Source sweep** — P/Invoke, registry, COM, Windows identity, desktop
   dependencies, CAS, and conditional-compilation symbols over the whole tree.

The 160-file shipped closure rendering the frozen template and serving the
ASMX JSON chain on three OSes is standing behavioral evidence for the already
compiled surface; no new runtime probes were needed because the remaining
distance is wiring and configuration, not serializer or BCL semantics.

## Build-recipe facts the compile surfaced

- **`WEB_EXTENSIONS_CODE` must be defined.** The `Compilation/WCFModel` and
  `Compilation/XmlSerializer` files are VS-designer shared sources; without
  the symbol they compile as `Microsoft.VSDesigner.*` against VS-only
  resources. The shipped closure never noticed because it compiles none of
  them. Other symbols in the tree (`INDIGO`, `ATLAS_DEV`, `ORYX_VNEXT`,
  `ENABLE_WCF_SUPPORT`, `ENABLE_LINQ_PARTIAL_TRUST`) stay undefined,
  matching the shipped 4.8.1 assembly.
- **`ui/WebControls/LinqDataSourceContextData.cs` and
  `ContextDataSourceContextData.cs` both define
  `ContextDataSourceContextData`** (same type, same copyright header naming
  the former; the latter is the trimmed duplicate). Exactly one belongs in
  any compile list.
- `Properties/AssemblyInfo.cs` compiles, but the repo project keeps its own
  `AssemblyInfo.cs`/`AssemblyIdentity.cs`; the tree copy stays excluded.
- `Script/Services/ProxyGenerator.cs` misses only
  `AssemblyRef.SystemServiceModelWeb`, a constant the in-repo
  `Rehost.WebForms.ReferenceSource.BuildInputs` copy does not carry — a
  repo-side gap, not a BCL gap. The file itself serves `.svc` proxy scripts
  and stays with the WCF exclusion.

## Empirical .NET 10 findings

Present (proven by the green build, no shim):

| API family | Where it came from |
|---|---|
| `ServiceContractAttribute`, `OperationContractAttribute`, `ServiceKnownTypeAttribute`, `ContractDescription`, `ServiceEndpoint`, `System.ServiceModel.Channels.Binding`, `ConcurrencyMode`, `KeyedByTypeCollection<T>` | `System.ServiceModel.Primitives` 8.1.2 |
| `XsdDataContractImporter` | `System.Runtime.Serialization.Schema` 10.0.10 |
| OleDb object model (compiles everywhere; Windows-only at runtime) | `System.Data.OleDb` 10.0.10 |
| Everything the 160-file closure and the ScriptModule/app-services/query stacks consume from `System.Web` internals | Runtime friend access, already in place |

Missing (every shim, grouped by the Framework assembly that owned it):

| Missing API | Framework home | Files touching it |
|---|---|---|
| `ServiceHost`, `ServiceBehaviorAttribute`, `InstanceContextMode` | `System.ServiceModel` (server side; absent from the modern client packages) | `ApplicationServices/{ApplicationServicesHostFactory,AuthenticationService,ProfileService,RoleService}.cs` |
| `ServiceHostFactory`, `AspNetCompatibilityRequirements(Attribute/Mode)` | `System.ServiceModel.Activation` (no modern equivalent) | same four |
| `WsdlImporter`, `IWsdlImportExtension`, `IPolicyImportExtension`, `MetadataSet`, `MetadataSection`, `MetadataConversionError`, `Wsdl{Contract,Endpoint}ConversionContext`, `ServiceContractGenerator`, `ServiceContractGenerationOptions`, `XmlSerializer/DataContractSerializer MessageContractImporter`, `FaultImportOptions`, `XmlSerializerImportOptions`, `WrappedOptions` | `System.ServiceModel` metadata/codegen (dotnet-svcutil vendors its own fork) | `Compilation/WCFBuildProvider.cs`, `Compilation/WCFModel/{VSWCFServiceContractGenerator,MetadataFile,HttpBindingExtension,AsmxEndpointPickerExtension,ProxyGenerationError}.cs` |
| `ServiceModelSectionGroup`, `ClientSection`, `MetadataElement`, `ChannelEndpointElement` | `System.ServiceModel` configuration object model | `VSWCFServiceContractGenerator.cs` |
| `DataServiceContext` | `System.Data.Services.Client` (WCF Data Services, retired) | `WCFBuildProvider.cs` |
| `EntityClassGenerator`, `LanguageOption` | `System.Data.Services.Design` (retired) | `WCFBuildProvider.cs` |
| `DataSetSchemaImporterExtension`, `TypedDataSetSchemaImporterExtensionFx35` | `System.Data`/`System.Design` (same cut the Web Services analysis hit) | `WCFBuildProvider.cs`, `VSWCFServiceContractGenerator.cs` |
| `DataContext`, `ITable`, `RefreshMode`, `Meta{Model,Table,Type,DataMember}`, `UpdateCheck` | `System.Data.Linq` (LINQ to SQL, never ported) | `ui/WebControls/{ILinqToSql,LinqToSqlWrapper,LinqDataSourceView}.cs` |
| `System.Windows.Forms.Application.UserAppDataPath` | WinForms | `ClientServices/ConnectivityStatus.cs`, `ClientServices/Providers/SqlHelper.cs` |
| `AppDomain.DefineDynamicAssembly` | removed API; the modern entry point is static `AssemblyBuilder.DefineDynamicAssembly` — a one-line portable seam, not a gap | `ui/WebControls/Dynamic.cs` |

`ClientServices/Providers/ClientSettingsProvider.cs` fails only on a dead
`using System.ServiceModel.Activation;` directive — no Activation type is
used in the file.

## The clean cut

The 111 files outside today's compile list split as:

| Group | Files | Lines | Verdict |
|---|---|---|---|
| `ScriptModule` + internal JSON application services (`Profile/ProfileService.cs`, `Security/{Authentication,Role}Service.cs`) | 4 | 438 | **Portable, zero shims** — see the correction below |
| ApplicationServices event args + `KnownTypesProvider`, `Management/WebServiceErrorEvent.cs` | 6 | ~300 | Portable, zero shims |
| Query stack: `Expressions/*`, `QueryExtender`, `QueryableDataSource*`, `ContextDataSource*`, `DynamicData/*` contracts, `Dynamic.cs`, helper/event-args files | ~35 | ~4,700 | Portable; `Dynamic.cs` needs the one-line `AssemblyBuilder` seam |
| `LinqDataSource` family | 15 | 2,154 | Control/event-args files portable, but the three files above hard-require `System.Data.Linq`; the control cannot compile without its view |
| WCF proxy generation: `Compilation/**` | 30 | 10,064 | 24 model files portable (under `WEB_EXTENSIONS_CODE`); 6 files hold every missing WCF/Data-Services/DataSet API |
| WCF hosting of application services: `ApplicationServices/{HostFactory,Authentication,Profile,Role}Service.cs` | 4 | 662 | Blocked on server-side WCF (`ServiceHost`, Activation) |
| Client Services: `ClientServices/**` | 14 | 3,810 | Compiles (with the two shims + dead using) but Windows-desktop-coupled at runtime |
| `Properties/AssemblyInfo.cs`, `Resources/WCFModelStrings.Designer.cs`, `ProxyGenerator.cs`, duplicate-type file | 4 | — | Build-recipe items, not features |

No file outside `ApplicationServices` (WCF half), `Compilation`,
`ClientServices`, and the three LINQ-to-SQL files touches any missing API.

## Correction: the JSON application services are not WCF-hosted

`WebServiceData.GetApplicationService` maps the built-in root names
(`Profile_JSON_AppService.axd`, `Authentication_JSON_AppService.axd`,
`Role_JSON_AppService.axd`) to `System.Web.Profile.ProfileService`,
`System.Web.Security.AuthenticationService`, and
`System.Web.Security.RoleService`
(`Script/Services/WebServiceData.cs:63`) — the **internal `[ScriptService]`
classes** in `Profile/` and `Security/`, served through the same
`ScriptHandlerFactory`/`RestHandler` chain as any script service, calling
`Membership`/`Roles`/`ProfileBase` from the runtime. They compile with zero
shims. The WCF-hosted services are the *public*
`System.Web.ApplicationServices.{Authentication,Profile,Role}Service`
classes reached only through an explicit `.svc` + `ApplicationServicesHostFactory`
deployment, which the port does not carry.

The 2026-08 import gated the built-in mappings behind `#if NETFRAMEWORK` with
the reason "WCF-hosted and not compiled"
([provenance](../provenance/system-web-extensions.md)); the
[web-services follow-up](../follow-ups/web-services.md) repeats the claim.
Both conflate the two surfaces. The AJAX-facing JSON services (what
`Sys.Services.*` and `MicrosoftAjaxApplicationServices.js` call) are ordinary
porting work; only the `.svc` WCF hosting is blocked.

## Windows-coupled code (all of it)

Everything is inside `ClientServices/` — the desktop/offline client-side
provider stack (WinForms-era "Client Application Services"):

- **P/Invoke:** exactly two, `wininet.dll` `InternetGetCookieW`/`InternetSetCookieW`
  (`Providers/ProxyHelper.cs:446`), sharing IE's cookie jar with the current
  Windows user session.
- **Windows identity:** `WindowsIdentity`/`WindowsPrincipal` in
  `ClientWindowsAuthenticationMembershipProvider`, `ProxyHelper`,
  `ClientFormsAuthenticationMembershipProvider`.
- **Desktop paths:** `System.Windows.Forms.Application.UserAppDataPath` for
  the offline-state and SQL CE cache locations (`ConnectivityStatus.cs:30`,
  `Providers/SqlHelper.cs`).
- **OleDb/SQL CE:** the offline credential/role/settings cache speaks OleDb
  to SQL Server Compact in five provider files, falling back to isolated
  storage.

The rest of the assembly: no P/Invoke, no registry (zero `Microsoft.Win32`
uses anywhere in the tree), no COM (`ComVisible(false)`).

## CAS

14 declarative attribute sites in 10 files, inert with the
`System.Security.Permissions` package. Zero live imperative CAS: both
`ReflectionPermission.Assert()` calls sit behind the never-defined
`ENABLE_LINQ_PARTIAL_TRUST` (`ui/WebControls/Dynamic.cs:272,315`), and every
`PermissionSet.Assert` in Client Services is commented out in the pinned
source.

## Dead by default

- **`WCFBuildProvider`** — the 4.8.1 root web.config registers **no**
  `.svcmap`/`.datasvcmap` build provider
  ([buildProviders](../../third_party/microsoft/framework-config/web.config#L97));
  Visual Studio's web-site tooling registered it per-application. An
  application that never carries such a registration never constructs the
  type.
- **`ApplicationServicesHostFactory`** — reached only from a `.svc` file
  naming it; the port maps no `.svc` handler.
- **Client Services** — activated only by explicit
  `ClientFormsAuthenticationMembershipProvider` (or sibling) provider
  registrations in an application config; a web application never carries
  them (the stack exists for desktop clients).

## Framework configuration closure (the gap that matters)

What 4.8.1 registers for this assembly versus the portable baseline today:

| Framework registration | Framework source | Baseline today |
|---|---|---|
| `system.web.extensions` section group: `scripting/scriptResourceHandler`, `scripting/webServices/{jsonSerialization,profileService,authenticationService,roleService}` | [machine.config](../../third_party/microsoft/framework-config/machine.config#L126) | **absent** — an application web.config carrying the section group (common in AJAX-era apps) fails as an unrecognized section; the section types themselves already compile |
| `<compilation><assemblies>` entry | [web.config](../../third_party/microsoft/framework-config/web.config#L89) | present (`Rehost.WebForms.Extensions`) |
| `*_AppService.axd` → `ScriptHandlerFactory`, `validate="False"` | [web.config](../../third_party/microsoft/framework-config/web.config#L173) | **absent** |
| `ScriptResource.axd` → `ScriptResourceHandler` | web.config L174 | present |
| `*.asmx` → `ScriptHandlerFactory` | web.config L178 | present |
| `ScriptModule-4.0` module | [web.config](../../third_party/microsoft/framework-config/web.config#L243) | **absent** — with it absent, async-postback error formatting, page-method routing, and app-service authorization skips never run |
| `<pages><controls>` for `System.Web.UI` + `System.Web.UI.WebControls` | web.config L348 | present |
| `<pages><controls>` for `System.Web.UI.WebControls.Expressions` | [web.config](../../third_party/microsoft/framework-config/web.config#L350) | **absent** (namespace not compiled yet) |

## Workload weighting

| Workload | Prevalence in template-era apps | Verdict on .NET 10 |
|---|---|---|
| `UpdatePanel`/async postbacks, `UpdateProgress`, `Timer` | dominant — the signature AJAX feature | **Compiles today**; needs `ScriptModule-4.0` registration + evidence. The wire protocol is `PageRequestManager` (compiled) + `MicrosoftAjaxWebForms.js` (embedded) |
| Page methods (`[WebMethod]` statics on pages, `PageMethods` proxy) | common | **Compiles today** (RestHandler chain shipped); routing lives in `ScriptModule.OnPostAcquireRequestState` — inactive until the module is registered |
| ASMX script services (`[ScriptService]`, `/js` proxies) | common | Shipped (2026-08-21) |
| `JavaScriptSerializer`, `ScriptManager`/references/CDN/bundles, `ListView`/`DataPager` | dominant | Shipped |
| JSON application services (`Sys.Services.AuthenticationService` et al.) | uncommon but present in AJAX-era apps | Portable, zero shims (see correction) |
| History (`EnableHistory`, `Sys.Application` navigation) | rare | Types + script shipped; evidence only |
| `QueryExtender`/`QueryableDataSource`/Expressions | uncommon | Portable; one-line `AssemblyBuilder` seam in `Dynamic.cs` |
| `LinqDataSource` | moderate in 2008–2012 apps | Control markup parses only if compiled, but the runtime hard-requires `System.Data.Linq`, which has no modern port — an app using it cannot run regardless; absence (compile-time failure) is the honest boundary. WebFormsForCore reached the same verdict (`NETFRAMEWORK`-only) |
| `.svcmap`/`.datasvcmap` WCF proxy generation | rare (VS web-site tooling) | Blocked on the WCF metadata/codegen stack; dead by default |
| Client Application Services | rare (desktop apps only) | Windows-desktop-coupled; not a web-server workload at all |
| WCF-hosted application services (`.svc`) | rare | Blocked on server-side WCF; the AJAX-facing equivalents are the portable JSON services |

## Maximum portable surface (tiers)

- **T1 — AJAX request-path activation** (438 lines: `ScriptModule`,
  `Profile/ProfileService.cs`, `Security/{Authentication,Role}Service.cs`,
  reverting the `WebServiceData.cs` `#if NETFRAMEWORK` gate): async
  postbacks, page methods, JSON application services, plus the four missing
  baseline-config registrations above. Zero new APIs; the work is config
  closure, deployment promise, and evidence. First-reach risk: the
  environment-derived statics pattern
  ([ambient-statics-audit](../follow-ups/ambient-statics-audit.md)) on the
  partial-rendering path (`HttpResponse.SwitchWriter`, redirect
  interception, the section caches `AppLevelCompilationSectionCache` /
  `DeploymentSectionCache` / `CustomErrorsSectionWrapper`).
- **T2 — query/data-source stack** (~4,700 lines): Expressions namespace,
  `QueryExtender`, `QueryableDataSource`, `ContextDataSource`, DynamicData
  contracts, `Dynamic.cs` with the `AssemblyBuilder` seam. Fully portable;
  adds the `Expressions` `<controls>` registration.
- **T3 — `LinqDataSource`** (2,154 lines): excluded while `System.Data.Linq`
  has no modern implementation; types stay absent so consumers fail at
  compile time. Trigger: a real application that ships LINQ-to-SQL — which
  would need a vendored LINQ-to-SQL before this control matters.
- **T4 — WCF proxy generation** (10,064 lines) and **WCF-hosted application
  services** (662): excluded; blocked on the WCF metadata/codegen and
  server-hosting stacks. CoreWCF exists for hosting (different namespaces —
  source adaptation, not a reference swap); dotnet-svcutil's `FrameworkFork`
  is the codegen precedent. Trigger: a real application.
- **T5 — Client Services** (3,810 lines): excluded as non-web,
  Windows-desktop-coupled (the assembly's only P/Invokes, WinForms paths,
  Windows identity, OleDb).

## Behavioral deltas on .NET 10 (compile-clean but different)

- **`ClientCultureInfo`** serializes `DateTimeFormat`/`NumberFormat` for the
  client (`Sys.CultureInfo`); modern .NET sources culture data from ICU, not
  NLS, so emitted patterns can differ from Framework and between OSes.
  Test baselines must not assume byte-identical culture payloads.
- `ScriptResourceHandler` URL protection rides `Page.EncryptString` — the
  machine-key seams already shipped; cross-machine URL stability follows the
  configured keys, as on Framework.
- `PageRequestManager` writes the async-postback wire format through
  `HttpResponse.SwitchWriter` and internal hidden-field inventory — Runtime
  internals already imported; deltas would be defects, not platform facts.
- No `Encoding.Default`, `ThreadAbortException`, or `TraceSource`
  dependencies anywhere in the tree (unlike `System.Web.Services`).

## Prior art

- [WebFormsForCore](https://github.com/webformsforcore/WebFormsForCore)
  compiles its `System.Web.Extensions` with CoreWCF packages for reached
  service behavior, rejects `.svcmap`/`.datasvcmap` generation in its build
  provider, and compiles `LinqDataSource` only for `NETFRAMEWORK` — the same
  three cut lines this analysis lands on.
- [dotnet-svcutil](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-svcutil-guide)
  vendors the WCF metadata-import/codegen stack privately — the existence
  proof and cost signal for T4, exactly parallel to the Web Services T4
  finding.
- [CoreWCF](https://github.com/CoreWCF/CoreWCF) provides modern
  `ServiceHost`-equivalent hosting under `CoreWCF.*` namespaces; adopting it
  is a source adaptation of the four hosting files, not a reference swap.

## Relationship to current state

[compatibility.md](../compatibility.md) lists the assembly as Partial with
"async postbacks and cross-platform gates remain open". This analysis grounds
that: the open features are compiled-but-unwired (T1), not missing. The
inventory's porting-assessment row "AJAX services/application services —
hard; ASMX/WCF interaction" splits cleanly: the ASMX half shipped, the JSON
application services are portable zero-shim work mislabeled as WCF (see
correction), and only `.svc` hosting is genuinely WCF-blocked. The
`#if NETFRAMEWORK` gate in `WebServiceData.cs` and the
[web-services follow-up](../follow-ups/web-services.md) JSON-application-services
bullet both need their reasons rewritten when T1 lands.
