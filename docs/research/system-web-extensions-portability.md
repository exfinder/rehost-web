# `System.Web.Extensions` portability analysis

Port-distance analysis of the pinned 271-file
[source tree](../../src/System.Web.Extensions.ReferenceSource/). The companion
[inventory](system-web-extensions-inventory.md) owns size, assets, dependencies and Windows
coupling; this document owns compile evidence, clean exclusion lines and staged scope.

## Result

All 271 files compiled on .NET 10 against the three Rehost assemblies with
`WEB_EXTENSIONS_CODE`, four packages, one duplicate-file exclusion and shims for proven-missing
APIs. Missing APIs occur in only 16 files across WCF hosting/code generation, LINQ-to-SQL and
desktop Client Services. No AJAX request-path file uses them.

The important remaining distinction is activation, not type availability. Partial rendering,
page methods, JSON application services, Timer and History need no new API surface; relevant
types either already ship or compiled cleanly. They needed Framework configuration and behavior
evidence. T1/T2 have since landed; see [current state](#relationship-to-current-state).

## Method and build facts

A scratch project named `Rehost.Web.Extensions` compiled all sources against `net10.0`, the
Rehost.Web runtime/Services/ApplicationServices assemblies, the repo facade-removal target, and
`System.CodeDom`, ConfigurationManager, Security.Permissions, ServiceModel client packages,
Runtime.Serialization.Schema and OleDb. Iterative shimming ended at zero errors, proving all
unshimmed references present. A source sweep covered native calls, registry, COM, identity,
desktop dependencies, CAS and conditional symbols. The existing 160-file closure supplied
behavioral evidence for already shipped paths.

Build recipe:

- Define `WEB_EXTENSIONS_CODE`; otherwise WCF designer-shared files select VS-only namespaces.
  Leave `INDIGO`, `ATLAS_DEV`, `ORYX_VNEXT`, `ENABLE_WCF_SUPPORT` and
  `ENABLE_LINQ_PARTIAL_TRUST` undefined.
- Compile only one of `LinqDataSourceContextData.cs` and
  `ContextDataSourceContextData.cs`; both define `ContextDataSourceContextData`.
- Exclude source-tree `AssemblyInfo.cs`; the project owns identity.
- `ProxyGenerator.cs` also needs the absent build constant
  `AssemblyRef.SystemServiceModelWeb`; this stays with the WCF cut.

## Proven API boundary

Modern packages supply ServiceModel contracts/client descriptions and bindings,
`XsdDataContractImporter`, OleDb types, and everything used by the shipped/AJAX/query paths.
Missing APIs are localized:

| Stack | Missing surface | Files |
|---|---|---|
| WCF server hosting | `ServiceHost`, behavior/instance attributes, `ServiceHostFactory`, ASP.NET compatibility types | Four public `ApplicationServices/*Service.cs` files and host factory |
| WCF metadata/codegen/config | `WsdlImporter`, import extensions, metadata models, contract generator/options, importer options, ServiceModel configuration | `WCFBuildProvider.cs` and five `Compilation/WCFModel` files |
| Retired Data Services/design | `DataServiceContext`, `EntityClassGenerator`, language option, DataSet schema extensions | Same WCF compile path |
| LINQ-to-SQL | `DataContext`, `ITable`, mapping metadata, refresh/update types | `ILinqToSql.cs`, `LinqToSqlWrapper.cs`, `LinqDataSourceView.cs` |
| Desktop Client Services | WinForms `Application.UserAppDataPath` | `ConnectivityStatus.cs`, `SqlHelper.cs` |
| Removed API | `AppDomain.DefineDynamicAssembly` | `Dynamic.cs`; replace with static `AssemblyBuilder.DefineDynamicAssembly` |

`ClientSettingsProvider.cs` also has a dead ServiceModel.Activation `using`. Missing APIs do not
escape these areas.

## Clean cut

| Group outside the original closure | Size | Verdict |
|---|---:|---|
| `ScriptModule` + three internal JSON services | 4 files / 438 lines | Portable, zero shims |
| Application-service support/event types | 6 / ~300 | Portable, zero shims |
| Expressions, QueryExtender, query/context sources, DynamicData | ~35 / ~4,700 | Portable; one-line Reflection.Emit seam |
| `LinqDataSource` family | 15 / 2,154 | Three core files require unavailable LINQ-to-SQL |
| WCF proxy generation | 30 / 10,064 | Six files contain all missing APIs |
| WCF-hosted application services | 4 / 662 | Blocked on server-side WCF |
| Client Services | 14 / 3,810 | Compiles with shims; Windows-desktop-coupled at runtime |

### JSON services correction

AJAX JSON services are not the WCF-hosted public services. `WebServiceData` maps
`Profile_JSON_AppService.axd`, `Authentication_JSON_AppService.axd` and
`Role_JSON_AppService.axd` to internal `[ScriptService]` classes under `Profile/` and
`Security/`. They use the existing `ScriptHandlerFactory`/`RestHandler` chain and compiled
without shims. Only explicit `.svc` deployments of public
`System.Web.ApplicationServices.*Service` classes need WCF hosting. Earlier provenance and
follow-up wording conflated these surfaces; current docs/state use the corrected boundary.

## Platform and dead-by-default areas

The [inventory](system-web-extensions-inventory.md#platform-hotspots) records all Windows
coupling: it is confined to desktop Client Services. Fourteen declarative CAS sites are inert;
the only imperative asserts are behind undefined `ENABLE_LINQ_PARTIAL_TRUST` or commented out.

- `WCFBuildProvider` is inactive unless an application registers `.svcmap`/`.datasvcmap`.
- `ApplicationServicesHostFactory` requires an explicit `.svc` file and handler.
- Client Services requires explicit desktop-provider configuration.

These are safe exclusion seams, not hidden runtime fallbacks.

## Framework configuration closure

| Framework registration | Original source | Initial portable baseline |
|---|---|---|
| `system.web.extensions` section group and script/web-service subsections | [machine.config](../../third_party/microsoft/framework-config/machine.config#L126) | Absent |
| Extensions compilation assembly | [web.config](../../third_party/microsoft/framework-config/web.config#L89) | Present under Rehost identity |
| `*_AppService.axd` → `ScriptHandlerFactory` | [web.config](../../third_party/microsoft/framework-config/web.config#L173) | Absent |
| `ScriptResource.axd`, `*.asmx` | same source | Present |
| `ScriptModule-4.0` | [web.config](../../third_party/microsoft/framework-config/web.config#L243) | Absent |
| UI/WebControls page controls | [web.config](../../third_party/microsoft/framework-config/web.config#L348) | Present |
| Expressions page controls | [web.config](../../third_party/microsoft/framework-config/web.config#L350) | Absent |

This explained why compiled types alone did not activate async-postback error handling, page
method routing, application-service authorization or query-expression markup.

## Scope tiers

| Tier | Surface | Decision |
|---|---|---|
| T1 | AJAX activation: module, internal JSON services, routes/config | Portable; zero new APIs. Landed. |
| T2 | Query/data-source stack | Portable after Reflection.Emit seam. Landed. |
| T3 | `LinqDataSource` | Excluded while LINQ-to-SQL is unavailable; compile-time failure is explicit. |
| T4 | WCF proxy generation and `.svc` application services | Excluded; requires metadata/codegen fork or adapted server hosting. |
| T5 | Client Services | Excluded as non-web and Windows-desktop-coupled. |

The dominant AJAX workloads—UpdatePanel, page methods, ASMX script services, JSON services,
ScriptManager and controls—fall in landed T1/T2 or the earlier closure. WCF tooling, Client
Services and `.svc` application services were rare/dead-by-default. `LinqDataSource` requires a
full LINQ-to-SQL port, not an Extensions-local shim.

Pinned Reference Source contains LINQ-to-SQL (98 files / 43k lines) and much of WCF
(1,700+ files), but not Data Services client/design. Vendoring either is a separate product-sized
compatibility project. [dotnet-svcutil](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/dotnet-svcutil-guide)
privately vendors WCF metadata/codegen; [CoreWCF](https://github.com/CoreWCF/CoreWCF) offers
different-namespace hosting. Both are precedent, not drop-in references.

## Known modern-runtime deltas

- Client culture payloads may differ because modern .NET uses ICU rather than Framework NLS.
- Script-resource URL stability follows configured machine keys, as on Framework.
- Partial rendering uses imported Runtime writer/hidden-field internals; observed differences
  there are defects, not accepted platform variation.

## Relationship to current state

T1/T2 activation landed: ScriptManager, release scripts, async postbacks, page methods, and
disabled-by-default JSON application-service routes are listed in the
[compatibility map](../compatibility.md). Enabled services, debug/localized scripts,
LINQ-to-SQL, WCF and Client Services remain in the
[Extensions follow-up](../follow-ups/extensions-ajax-activation.md).
