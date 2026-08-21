# System.Web.Extensions source

The import authority is the sibling `../referencesource` checkout at revision
`ec9fa9ae770d522a5b5f0607898044b7478574a3`. Its `System.Web.Extensions` tree
object is `4980d036e77731ef47248b6c5d849acb5b69cefe`; all 368 files are copied to
`src/System.Web.Extensions.ReferenceSource`. The shared Reference Source MIT
license remains at `third_party/microsoft/referencesource/LICENSE.txt`.

Two files differ from that tree:

| File | Change | Reason |
| --- | --- | --- |
| `ui/ScriptControlManager.cs` | qualify six `OrderedDictionary<,>` uses as `System.Web.Util.` | .NET 9 added `System.Collections.Generic.OrderedDictionary<TKey,TValue>`; both namespaces are imported, so the bare name is ambiguous. A `using` alias cannot name an open generic. |
| `ClientServices/Providers/ClientData.cs` | isolated-storage cache filename built with `Path.Combine` instead of a `"\\"` concat | A literal backslash is not a separator off Windows. The file is not compiled; fixed during the 2026-08-21 backslash sweep so a ClientServices port does not inherit it. |

`Script/Services/WebServiceData.cs` carried a second deviation from 2026-08 —
the three built-in `*_JSON_AppService.axd` mappings behind `#if NETFRAMEWORK`,
recorded as "WCF-hosted and not compiled". The
[portability analysis](../research/system-web-extensions-portability.md)
disproved that reason (they map to the internal `[ScriptService]` classes in
`Profile/` and `Security/`, served through the ASMX JSON chain); the gate was
reverted and the file matches the tree again.

## Generated Microsoft AJAX scripts

The tree carries script sources and build recipes, not the built scripts the
Framework assembly embeds. Thirteen `.jsa` recipes under `Script` drive a
preprocessor whose entire vocabulary is `#include "path"`, `#if SYMBOL`,
`#else`, `#endif`, and a `##SYMBOL <code>` line form. Every file is
self-balanced and nesting never exceeds one level; include paths are
backslash-separated and resolve against `Script`. Nine inputs carry a UTF-8 BOM
that a naive reader splices into the middle of the output.

`eng/GenerateAjaxScripts.cs` runs the recipes with `dotnet run`, minifies, and
writes `src/Rehost.WebForms.Extensions/Scripts`. No symbol is defined, which
yields the 13 release names the assembly declares. `COPYRIGHT` stays undefined:
the shipped script carries one generated banner whose rule length follows the
copyright line, not the 62 per-file headers. `DEBUGINTERNAL` never ships.

Output is committed rather than generated during the build, because the import
is a pinned snapshot. That diverges from
[generated build inputs](generated-build-inputs.json), which regenerates into
`obj`; the guard here is `AjaxScriptResourceTests`.

Minification uses AjaxMin 4.12, the era-contemporary build of the tool the
Framework used, with `CrunchAll`, single-line output, and four tree
modifications suppressed: numeric-exponent and boolean shortening, `if` to
short-circuit, and `var` folded into `for`. Those were settled by reading a
shipped 4.8.9319.0 `System.Web.Extensions`. `MicrosoftAjaxTimer.js` reproduces
byte-for-byte and the set stays within one percent in aggregate; the remainder
diverges where one kill bit covers two transforms Framework treated separately.
The banner's CRLF is why `.gitattributes` exempts this directory from `eol=lf`.

The scripts read `Sys.Res` but never define it, in the drop and in Framework's
own embedded copies alike. `ScriptResourceHandler` appends it per request from
the `ScriptLibrary` resources named by `[ScriptResource]`, so the strings exist
only on the `ScriptResource.axd` path; a mapping that serves the file directly
leaves them undefined, as the Framework template packages also do.

No `.debug.js` is produced. Framework's debug build is a code generator, not a
preprocessor run: it synthesizes `Function._validateParams` calls from the
`/// <param>` and `/// <value>` comments, hoists prototype members into
`$`-mangled named functions, and assigns `locid` attributes. That tool is not in
the drop. `ScriptReference.ShouldUseDebugScript` falls back to the release
script under the default `ScriptMode.Auto`, so an absent debug resource degrades
quietly where a preprocessed-but-unvalidated one would mislead.

`Rehost.WebForms.Extensions` compiles the closure the frozen templates reach,
plus `Script/Services` (the ASMX JSON chain: `ScriptHandlerFactory`,
`RestHandler`, the client proxy generators) against
`Rehost.WebForms.WebServices`. `Script/Services/ProxyGenerator.cs` stays out:
it generates proxies for WCF service endpoints and only the uncompiled
`WCFBuildProvider` reaches it.
`WebFormsIdentityApplication` added the second slice: `ListView` and the
`DataPager` family it names (`ListView*`, `DataPager*`, the three pager fields,
`IPageableItemContainer`, `InsertItemPosition`, `PageEventArgs`,
`PagePropertiesChangingEventArgs`), which the account pages declare through
`OpenAuthProviders.ascx`.

The [AJAX activation plan](../follow-ups/extensions-ajax-activation.md)
(2026-08-21) added the third slice: `ScriptModule`, the internal JSON
application services (`Profile/ProfileService.cs`,
`Security/{Authentication,Role}Service.cs`) with their ApplicationServices
event-args siblings, `Management/WebServiceErrorEvent.cs`, and the query
stack (`ui/WebControls/Expressions/*`, `Query*`, `ContextDataSource*`,
`DataSourceHelper`, `Dynamic`, `DynamicQueryableWrapper`, `IDynamicQueryable`,
`IQueryableDataSource`, `DynamicData/*`). `Dynamic.cs` binds
`AppDomain.DefineDynamicAssembly`, which modern .NET removed; the assembly's
own `AppDomainDynamicAssembly.cs` extension method maps the call to the
static `AssemblyBuilder.DefineDynamicAssembly`, keeping the imported file
byte-identical.

Still out, per the plan's exclusions: the `LinqDataSource` family plus
`ILinqToSql`/`LinqToSqlWrapper` (`System.Data.Linq` has no modern
implementation), `Compilation/**` (WCF `.svcmap` proxy generation), the WCF
`.svc` application-service hosts (`ApplicationServices/{ApplicationServicesHostFactory,AuthenticationService,ProfileService,RoleService}.cs`),
`ClientServices/**` (Windows-desktop stack), `PermaLink.cs` (fully commented
out upstream), and `LinqDataSourceContextData.cs` (duplicate definition of
`ContextDataSourceContextData`). Excluded
public APIs are absent rather than stubbed. Design-time metadata the closure
still names is carried by internal shapes in the runtime's
`Compatibility/DesignTime`, which keeps the control declarations byte-for-byte;
see [dependency decisions](../dependency-decisions.md).
