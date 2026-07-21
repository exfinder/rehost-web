# AppDomain, remoting, and CAS architecture research

**Status:** architecture for all 34 build errors approved and implemented.
Mandated Runtime build: **0 errors / 2,397 warnings**. Runtime loading,
shutdown/recycle, process restart, and hot reload remain TODOs. No tests or
commit performed.

## Approved decisions

- One Web Forms application/current AppDomain per OS process; no secondary
  AppDomains or `AssemblyLoadContext` emulation.
- APIs explicitly requiring a new worker AppDomain, including
  `ApplicationHost.CreateApplicationHost` and worker-domain
  `ApplicationManager` paths, throw actionable `PlatformNotSupportedException`.
  Viable current-application inspection and object registration remain local.
- `ClientBuildManager` is initially unsupported. In-application `BuildManager`
  runtime compilation remains in scope. Future precompilation should use a
  separate process and may integrate into `dotnet build`; preserve that product
  direction when defining build/runtime boundaries.
- Runtime execution is always full trust. Missing/default/explicit `Full` trust
  starts normally. Explicit non-Full trust or `legacyCasModel="true"` fails
  early with actionable migration guidance rather than silently broadening
  privileges. Inert CAS metadata may produce diagnostics without blocking
  startup. A future explicit Rehost override may acknowledge the risk and force
  full trust; no implicit override is permitted.
- `ApplicationManager` initializes the one supported application locally:
  validate that no different application is already bound, bind the legacy
  application identity before first `HttpRuntime` access, construct one local
  `HostingEnvironment`, then use its existing local object-registration path.
  A second/different application fails explicitly. This does not relax the
  rejection of `ApplicationHost.CreateApplicationHost` or other APIs whose
  contract requires a new worker AppDomain.
- Existing `ObjectHandle` signatures remain for supported local paths.
  `Unwrap()` returns the stored local object reference; it does not create a
  proxy, copy/serialize the value, provide a lease, or establish isolation.
  Worker-domain APIs cannot use this local behavior to claim support.
- `RecycleLimitMonitor` uses one direct process singleton for the one
  application instead of default-domain `DoCallBack` and a remoting proxy.
  This approves coordination topology only; the underlying Windows-bound
  process-memory measurement still requires a separate portable disposition.
- Remoting-only `RemotingServices.Disconnect` calls are removed, not shimmed.
  Supported local shutdown explicitly clears manager/environment ownership;
  ordinary references and GC own lifetime. `ClientBuildManager` disconnect
  paths are unreachable because that feature is initially unsupported.
- Remoting lease overrides remain unchanged in imported source and continue to
  return Framework-compatible `null`. They are documented as inert local
  compatibility surface: the source contains no internal call sites and no
  remoting boundary can be created. Actual proxy/lease/remoting entry points
  remain explicitly unsupported; no 22-file imported-source rewrite is made.
- The two permission-protected `DataDirectory` writes become ordinary
  two-argument `AppDomain.CurrentDomain.SetData` calls. Runtime full trust makes
  the removed CAS permission metadata inapplicable. Exact imported call sites:
  `HttpRuntime.cs:350-351` and `DataAccess/SqlConnectionHelper.cs:119`.
- `SecurityUtils.DemandGrantSet(Assembly)` retains its callers and changes only
  its body to `MemberAccessPermission.Demand()`. This removes the unavailable
  `Assembly.PermissionSet` API while accepting that all retained CAS demands
  are inert compatibility behavior on modern .NET. Exact imported range:
  `misc/SecurityUtils.cs:71-76`.
- The two dynamic-assembly call sites use static
  `AssemblyBuilder.DefineDynamicAssembly(name, Run)`. Existing emit
  locks remain; assemblies stay in-process, executable, and non-persisted.
  Exact imported ranges: `Util/FactoryGenerator.cs:127-137` and
  `Util/FastPropertyAccessor.cs:93-103`.
- The obsolete Fusion shadow-copy cleanup block is removed from
  `Compilation/BuildResultCache.cs:941-946`, including the
  `UnsafeNativeMethods.DeleteShadowCache` call. Rehost's preceding build-output
  cleanup remains unchanged; no replacement cache paths are invented.
- `TrustSection.Level` keeps its imported default of `Full`. During hosting
  initialization, `HttpRuntime` accepts that default or explicit `Full`; any
  other level, or `legacyCasModel=true`, throws `PlatformNotSupportedException`
  before policy-file parsing. The unavailable `SetAppDomainPolicy` call is
  removed from the now-unreachable legacy method. Remaining imported CAS policy
  text may remain unreachable to minimize provenance changes. Exact imported
  areas: `HttpRuntime.cs:472-493,3125-3238`.
- `ApplicationManager` remains imported rather than being replaced. Its existing
  worker-domain `CreateAppDomainWithHostingEnvironment` implementation remains
  under `NETFRAMEWORK`; the modern branch binds or validates the current-domain
  application identity, rejects ClientBuildManager hosting, directly constructs
  and initializes one `HostingEnvironment`, and returns it. The explicitly
  worker-domain `CreateInstanceInNewWorkerAppDomain` entry point throws. The
  now-unused `PopulateDomainBindings`, `GetDefaultDomainIdentity`, and
  `AppDomainSwitches` helpers remain under `NETFRAMEWORK`. Together with the
  already approved disconnect removal, this addresses all 22 diagnostics without
  replacing the class. Exact imported areas:
  `Hosting/ApplicationManager.cs:824-840,885-1325,1667-1703,1769-1791` plus the
  disconnect at `732-740`.
- `ClientBuildManager.EnsureHostCreated` throws an actionable
  `PlatformNotSupportedException`; future precompilation belongs outside the
  runtime and may integrate with `dotnet build`. Its two remoting-only callback
  disconnect blocks are removed. Exact imported areas:
  `Compilation/ClientBuildManager.cs:487-498,572-583,647-663`.
- `RecycleLimitMonitor.GetSingleton` directly gets or creates the process-static
  `RecycleLimitMonitorSingleton` under its existing lock. No default-domain
  callback, AppDomain data, proxy, or lease is involved. Exact imported areas:
  `Hosting/RecycleLimitMonitor.cs:145-159,212-225`; its lease override remains
  unchanged under the approved no-code lease policy.
- AppDomain shutdown/recycle, container-process restart, and hot reload remain
  deferred TODOs. No `ShutdownAppDomain` or process-lifetime code changes are
  included in this build-error work. Current behavior must be revisited before
  runtime validation; this deferral addresses none of the 34 build errors.

## Evidence boundary

- Runtime checkpoint supplied/reproduced by main workstream: **34 errors / 2,419 warnings**.
- Error locations below come from the reproduced mandated build and agree with
  the 79-error inventory after removing the 45 diagnostics resolved by commits
  `248ec41..d924e80`.
- Imported source authority: Microsoft Reference Source
  `ec9fa9ae770d522a5b5f0607898044b7478574a3`; provenance:
  `docs/provenance/reference-source.json`.

## Baseline diagnostics

| Feature / file | Lines | Count | Framework dependency |
| --- | --- | ---: | --- |
| `Hosting/ApplicationManager.cs` | 738, 902, 938, 969, 1009, 1122, 1128, 1177, 1190, 1191, 1199, 1208, 1298 (2), 1673, 1674, 1675, 1676, 1677, 1680, 1691, 1788 | 22 | remoting disconnect; child-domain setup, CAS, activation, shadow copy |
| `Compilation/BuildResultCache.cs` | 944, 945 | 2 | CLR shadow-copy cache identity |
| `Compilation/ClientBuildManager.cs` | 496, 581 | 2 | remoting proxy disconnect |
| `Hosting/RecycleLimitMonitor.cs` | 152 (2) | 2 | callback from application domain to default-domain process singleton |
| `HttpRuntime.cs` | 3231 | 1 | legacy CAS policy installation on current domain |
| **AppDomain/remoting/hosting subtotal** |  | **29** |  |
| `Util/FactoryGenerator.cs` | 129 | 1 | `AppDomain.DefineDynamicAssembly` |
| `Util/FastPropertyAccessor.cs` | 95 | 1 | `AppDomain.DefineDynamicAssembly` |
| `DataAccess/SqlConnectionHelper.cs` | 119 | 1 | permission-protected AppDomain data |
| `HttpRuntime.cs` | 350 | 1 | permission-protected AppDomain data |
| `misc/SecurityUtils.cs` | 73 | 1 | target assembly CAS grant set |
| **Total** |  | **34** |  |

Dependency: hosting decision first; then remoting/lifetime; current-domain
codegen/loading; full-trust/CAS; mechanical dynamic emit.

## Framework behavior traced locally

- `ApplicationManager` creates one child AppDomain per application. It sets
  application base/configuration, private probing, shadow copy, compatibility
  switches, evidence, permission set, full-trust assemblies, and five ASP.NET
  data keys; then cross-domain activates `HostingEnvironment` and returns
  remoting `ObjectHandle`s (`ApplicationManager.cs:885-1325,1667-1702`).
- Public `ApplicationManager.CreateObject*` and
  `ApplicationHost.CreateApplicationHost` therefore promise a new worker domain,
  not local object construction (`ApplicationManager.cs:218-375,824-841`;
  `ApplicationHost.cs:29-58`).
- `ClientBuildManager` is explicitly caller-domain -> new application-domain
  orchestration. Clean rebuild unloads the old domain; callbacks are manually
  disconnected (`ClientBuildManager.cs:97-107,477-505,542-583,647-690`).
- `BuildResultCache.RemoveAllCodegenFiles` preserves the Fusion shadow-copy
  directory, then asks CLR to delete that cache (`BuildResultCache.cs:900-946`).
- `RecycleLimitMonitor` lives per application domain but registers into a
  process singleton in the default domain via `DoCallBack` and a remoting proxy
  (`RecycleLimitMonitor.cs:54-93,145-226`).
- `HttpRuntime` reads `.appDomain`, `.appId`, `.appPath`, `.appVPath`, and
  `.domainId` from current-domain data; these drive its public `AppDomainApp*`
  properties (`HttpRuntime.cs:317-338,2946-3078`). This part is viable without
  child domains if the host binds the keys before first `HttpRuntime` access.
- Framework three-argument `SetData` protects later `GetData` with the supplied
  permission. Both remaining callers protect `DataDirectory` using
  `FileIOPermission(PathDiscovery)` (`HttpRuntime.cs:346-352`;
  `SqlConnectionHelper.cs:97-122`).
- `SecurityUtils` first demands reflection access; only on denial does it demand
  the target assembly grant set plus restricted-member reflection
  (`SecurityUtils.cs:40-86`).
- Both emitters request in-memory `Run` assemblies with `isSynchronized: true`;
  generated assemblies intentionally live until process exit
  (`FactoryGenerator.cs:31-45,111-169`;
  `FastPropertyAccessor.cs:58-105,299-373`).

## Modern runtime facts

- Secondary domains are unsupported; `CreateDomain` throws
  `PlatformNotSupportedException`; `Unload` cannot unload. Microsoft recommends
  processes/containers for isolation. `AssemblyLoadContext` is a loading
  boundary, not an AppDomain/remoting/CAS equivalent.
  [Microsoft porting guidance](https://learn.microsoft.com/en-us/dotnet/core/porting/net-framework-tech-unavailable),
  [runtime `AppDomain` source](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/AppDomain.cs)
- Modern `AppDomain` is a singleton current-domain facade: ID 1, default and fully
  trusted. Current-domain assembly events, local activation, two-argument
  `GetData`/`SetData` remain. Data delegates to `AppContext`.
  [runtime `AppDomain` source](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/AppDomain.cs),
  [`AppContext.SetData`](https://learn.microsoft.com/en-us/dotnet/api/system.appcontext.setdata?view=net-10.0)
- Remoting is unavailable. `MarshalByRefObject` and `ObjectHandle` remain usable
  only as local objects/wrappers; leases, proxies, channels, cross-domain and
  cross-process behavior are not restored.
  [Microsoft porting guidance](https://learn.microsoft.com/en-us/dotnet/core/porting/net-framework-tech-unavailable),
  [`MarshalByRefObject`](https://learn.microsoft.com/en-us/dotnet/api/system.marshalbyrefobject?view=net-10.0),
  [`ObjectHandle`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.remoting.objecthandle?view=net-10.0)
- CAS is unsupported and not honored. `Assembly.PermissionSet` is Framework-only;
  current assemblies/domains report full trust. This is a runtime fact, not a
  sandbox.
  [`SYSLIB0003`](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib0003),
  [Framework `Assembly.PermissionSet`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assembly.permissionset?view=netframework-4.8.1),
  [runtime `Assembly` source](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Reflection/Assembly.cs)
- Modern in-memory emit uses static
  `AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run)`.
  `RunAndCollect` changes lifetime; `PersistedAssemblyBuilder` is only for saved
  output. Dynamic emit requires dynamic code.
  [`DefineDynamicAssembly`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilder.definedynamicassembly?view=net-10.0),
  [`AssemblyBuilder` guidance](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-reflection-emit-assemblybuilder)

## Hidden runtime blockers beyond 34 errors

| Imported call sites | Modern result | Required disposition |
| --- | --- | --- |
| `HttpRuntime.cs:989-995` (`SetDynamicBase`, `DynamicDirectory`) | setter no-op; no Framework dynamic directory | project-owned codegen path; otherwise startup can receive null path |
| `HttpRuntime.cs:1012-1033` (private path, shadow-copy path, cache path) | setters no-op | explicit loader/codegen behavior; document/reject unavailable shadow-copy semantics |
| `HttpRuntime.cs:1862-1905`, `ProcessHost.cs:1240`, `ApplicationManager.cs:1307` | unload throws | host-lifetime recycle or explicit unsupported exception; never queue failing unload loop |
| All `IsDefaultAppDomain()` branches | always true | audit default-domain assumptions; especially recycle monitor and cache-provider selection (`HostingEnvironment.cs:1460-1482`) |
| 22 `InitializeLifetimeService()` overrides returning null | silently pretend infinite remoting leases | local-only classification or explicit `PlatformNotSupportedException`; see exact list below |
| `ApplicationManager`, `ClientBuildManager`, `ApplicationHost` worker-domain APIs | cannot honor isolation/unload | reject at public boundary; do not inline execution |
| `<trust>`, `legacyCasModel`, security policy, host resolver, partial/full-trust assembly lists | CAS types may parse but enforcement absent | accept full trust only; reject CAS-affecting configuration during startup |
| 1,117 `SYSLIB0003` warnings | declarations/demands not enforced | later audited ledger; no blanket suppression |

Lease overrides: `ApplicationManager.cs:166`; `BuildManagerHost.cs:558`;
`ClientBuildManager.cs:605`; `ClientBuildManagerCallback.cs:37`;
`ClientBuildManagerTypeDescriptionProviderBridge.cs:19`;
`SimpleApplicationHost.cs:42`; `HostingEnvironment.cs:175`;
`RecycleLimitMonitor.cs:162,624`; `PreloadHost.cs:50`;
`ProcessHost.cs:422`; `ProcessHostFactoryHelper.cs:37`;
`ProcessProtocolHandler.cs:18`; `AppDomainProtocolHandler.cs:19`;
`IPipelineRuntime.cs:242`; `ISAPIApplicationHost.cs:93`;
`ISAPIRuntime.cs:107`; `MapPathBasedVirtualPathProvider.cs:242,320`;
`VirtualPathProvider.cs:28,313`; `WebAdminConfigurationHelper.cs:31`.

## Approved contract

Supported:

- one Web Forms application / `HostingEnvironment` per OS process;
- host binds current-domain app identity before Runtime static initialization;
- current-domain `HttpRuntime.ProcessRequest`, configuration, runtime compilation,
  application services, local `ObjectHandle.Wrap/Unwrap`, assembly events;
- full-trust execution only; `HttpRuntime.GetNamedPermissionSet()` remains null;
- in-memory non-collectible dynamic emit using modern APIs;
- current-domain hosting until shutdown/recycle is requested. Shutdown/recycle
  behavior is not validated and remains a documented TODO.

Explicitly unsupported:

- secondary AppDomains; multiple isolated apps per process; per-app unload,
  restart, shadow-copy/Fusion cache, or static-state reset;
- remoting proxies, leases, channels, remote activation/callbacks;
- `ApplicationHost.CreateApplicationHost`, worker-domain `ApplicationManager`
  creation, and initial `ClientBuildManager` implementation;
- partial trust, CAS policy, permission demands/asserts/permit-only semantics,
  `legacyCasModel`, host security resolver, CAS assembly lists;
- using `AssemblyLoadContext` as claimed AppDomain/security compatibility.

Required project-owned contracts:

1. Internal host-neutral current-application bootstrap: app ID, physical/virtual
   roots, config mapping, one-time initialization guard.
2. Deferred TODO: define a host-lifetime boundary before runtime validation;
   this build-error work adds no stop/restart contract.
3. One-app `ApplicationManager` compatibility surface: current app inspection and
   object creation work locally; every worker-domain creation path throws
   actionable `PlatformNotSupportedException`. Stop/restart remains a TODO.
4. Full-trust validation during `HttpRuntime` hosting initialization: reject
   non-Full trust and `legacyCasModel` before policy parsing.
5. Local object wrapper policy: retain `ObjectHandle` only where no proxy or lease
   behavior is implied.
6. Consistent unsupported-feature exception messages naming migration action.

## Approved imported-source deviations

Preferred option preserves the largest imported file unchanged:

| File/range | Proposed action | Alternative |
| --- | --- | --- |
| `Hosting/ApplicationManager.cs:824-840,885-1325,1667-1703,1769-1791` | **approved:** retain Framework branch; add current-domain modern branch; gate unused Framework helpers | whole-file project-owned replacement rejected |
| `Compilation/BuildResultCache.cs:941-946` | **approved:** remove obsolete Fusion shadow-cache cleanup block | exclude/copy whole file: disproportionate |
| `Compilation/ClientBuildManager.cs:487-498,572-583,647-663` | **approved:** host-required operations throw centrally; remove two proxy-only disconnect blocks | exclude file and reproduce public API: larger compatibility surface |
| `Hosting/RecycleLimitMonitor.cs:145-159,212-225` | **approved:** direct process-static singleton; no callback/proxy | exclude 627-line file: disproportionate |
| `HttpRuntime.cs:346-352` | **approved:** direct two-argument `SetData`; valid only after full-trust gate | project helper rejected as unnecessary |
| `DataAccess/SqlConnectionHelper.cs:97-122` | **approved:** same direct replacement | project helper rejected as unnecessary |
| `HttpRuntime.cs:472-493,3125-3238` | **approved:** default/explicit Full succeeds; non-Full or legacy CAS throws early; remove `SetAppDomainPolicy` call | deleting the full legacy branch causes a larger imported diff |
| `HttpRuntime.cs:988-1035` | **deferred TODO:** current-domain codegen/probing behavior | no change in build-error work |
| `HttpRuntime.cs:1862-1905` | **deferred TODO:** shutdown/unload behavior | no change in build-error work |
| `Util/FactoryGenerator.cs:127-137` | **approved:** static modern `AssemblyBuilder` overload; retain existing emit lock | none needed |
| `Util/FastPropertyAccessor.cs:93-103` | **approved:** same mechanical replacement; retain existing emit lock | none needed |
| `misc/SecurityUtils.cs:71-76` | **approved:** replace body with `MemberAccessPermission.Demand()`; retain callers | broader caller cleanup deferred |
| 22 lease overrides listed above | **approved:** no code change; document inert local compatibility | later public exposure audit |

Deferred rows are TODOs and unchanged in this build-error work.

## Dependency-ordered implementation groups

1. **Approve model:** one app/process; public failure boundaries; recycle outcome;
   initial `ClientBuildManager` disposition; full-trust configuration rule.
2. **Bootstrap + manager:** current-domain binding, one-app manager, reject worker
   domains. Mandated build; inception/checkpoint/deviation only.
3. **Remoting:** ClientBuildManager boundary, recycle singleton, lease/proxy audit.
   Mandated build; minimal compatibility doc.
4. **Shadow cache:** remove obsolete build-result Fusion cleanup. Mandated build.
5. **CAS/full trust:** startup rejection, two `SetData` replacements, reflection
   access rule. Mandated build.
6. **Dynamic emit:** two modern builder replacements; synchronization decision.
   Mandated build.
7. **Deferred runtime TODOs:** codegen/probing, shutdown/recycle, process restart,
   hot reload, portable memory measurement.
8. **Warning audit later:** CAS declarations/demands and remaining AppDomain/IIS
   call sites; narrow suppressions only after classification.

## Risks / deviations

- Process isolation/recycle is broader and more expensive than AppDomain
  isolation/recycle; in-process restart cannot reset all static state.
- One app/process breaks hosts using `ApplicationManager` for multiple sites.
- Initial `ClientBuildManager` rejection affects `aspnet_compiler`-style external
  precompilation; in-app `BuildManager` runtime compilation is separate.
- No shadow copy changes file replacement/locking and generated-assembly lifetime.
- Always-full-trust execution removes a historical defense boundary. OS/process
  identity, filesystem permissions, container/process isolation become security
  boundaries.
- Legacy CAS attributes remain source/API metadata but provide no enforcement;
  documentation must never imply otherwise.
- Dynamic emit blocks Native AOT and may require extra synchronization compared
  with Framework's synchronized assembly builder.

## Implementation validation

| Group | Errors | Warnings | Result |
| --- | ---: | ---: | --- |
| Current-AppDomain hosting bootstrap | 13 | 2,402 | 21 `ApplicationManager` diagnostics removed; remoting disconnect deferred to next group |
| In-process remoting compatibility | 8 | 2,402 | remaining disconnect and cross-domain callback diagnostics removed; `ClientBuildManager` rejects host creation |
| Fusion shadow-cache cleanup removed | 6 | 2,402 | two obsolete `AppDomainSetup` diagnostics removed; build-output cleanup retained |
| Full-trust CAS compatibility | 2 | 2,397 | four CAS/DataDirectory diagnostics removed; configured non-Full or legacy CAS fails early |
| Modern dynamic assembly creation | 0 | 2,397 | final two diagnostics removed; Runtime project emits successfully |
