# Clean .NET 10 build-error inventory

## Scope and guardrails

This document records the first clean attempt to compile untouched Microsoft
Reference Source for `System.Web` as `net10.0`. It was completed before
inspecting or comparing implementation choices in the previous POC. No source
was ported, no compatibility shim was added, no source file was excluded, and
no warning was suppressed.

The purpose of this pass is diagnosis, not a meaningful estimate of the final
porting workload. C# compilation reports failures in waves: once missing
generated inputs and high-fan-out references are restored, later method-body
and behavioral incompatibilities will become visible.

## Layered-diagnostic checkpoint correction

The July 20, 2026 compatibility pass confirmed that intermediate compiler
counts are frontier measurements, not remaining-work estimates. With unresolved
`System.Data.Design` declarations, the mandated build reported only 2 XSD
errors and 1,075 warnings. After replacing that provider with an explicit
unsupported contract, Roslyn advanced into method-body binding and reported 79
errors and 2,441 warnings.

The XSD change did not introduce these failures. Newly visible locations are
unrelated and include:

- removed AppDomain/AppDomainSetup hosting APIs;
- Windows impersonation and remote-configuration server APIs;
- missing ResX, OleDb, data-binding, and browser-capability design contracts;
- changed XML, SQL, cryptography, serialization, and reflection overloads.

Consequently, checkpoint counts may increase after a root blocker is removed.
Every compatibility change must report both diagnostics removed from its target
group and the complete newly exposed build result. “Few remaining errors” must
not be inferred from an unsuccessful compilation. A complete inventory is only
available after the project reaches emit successfully; until then, rerun the
mandated non-hanging build after every group and classify each newly exposed
wave before choosing implementation work.

## Provenance and immutable baseline

| Item | Recorded value |
| --- | --- |
| Upstream | `https://github.com/microsoft/referencesource.git` |
| Upstream revision | `ec9fa9ae770d522a5b5f0607898044b7478574a3` |
| Upstream revision date | 2025-10-15 21:56:37 +05:30 |
| Imported path | `System.Web` |
| Upstream/imported Git tree | `a4eb8e4ad3186fdacc2472803fce105146403f75` |
| Imported files | 1,920 |
| License | MIT, copyright Microsoft Corporation |
| Upstream license blob | `232e566b5f8a015c770c3fc41730593fb0758ef4` |
| License SHA-256 | `c1f47cf87974fdc14137ddd32e6273c0d9b30365bba4b96daf0b26243e309a4c` |
| Baseline commit | `e8a8e33b9f133b190f7b779b00c0a640d66face5` |

The imported tree ID exactly equals the upstream `System.Web` tree ID. The
machine-readable record is in `docs/provenance/reference-source.json`; the
unmodified license is in `third_party/microsoft/referencesource/LICENSE.txt`.

## Build harness and reproduction

The build harness was added in the separate commit
`4f070aa761ca348f2ad56840b5a73a97579193c6`. It uses SDK `10.0.302`, targets
only `net10.0`, compiles all 1,761 upstream `.cs` files, and makes only the
following build-system accommodations:

- disables SDK default compile-item discovery so the project can remain
  separate from the imported source;
- disables SDK-generated assembly metadata because Reference Source contains
  its own assembly metadata;
- disables implicit usings;
- enables unsafe code, which the original source requires.

It intentionally supplies no package references, feature constants,
preprocessed `.cspp` output, generated resources, source exclusions, or
compatibility code.

Build environment:

| Item | Value |
| --- | --- |
| Command | `dotnet build src/Rehost.WebForms.Runtime/Rehost.WebForms.Runtime.csproj --nologo --verbosity:minimal -flp:logfile=artifacts/build/net10.0/build.log;verbosity=diagnostic -bl:artifacts/build/net10.0/build.binlog` |
| .NET SDK | 10.0.302 |
| MSBuild | 18.6.11+35b593beb |
| Host runtime | 10.0.10 |
| Host | macOS 26.5, arm64 |
| Exit code | 1 |
| Elapsed | 23.33 seconds |

The raw diagnostic and binary logs remain local and are hash-recorded in
`artifacts/build/net10.0/build-metadata.json`. The tracked
`artifacts/build/net10.0/inventory/diagnostics.tsv` contains every reported
error and warning once, with its location, subsystem, category, and complete
message.

## Headline result

| Measure | Count |
| --- | ---: |
| Errors | 5,794 |
| Warnings | 755 |
| All diagnostics | 6,549 |
| Files with errors | 491 |
| Files with warnings | 163 |
| Unique error message groups | 241 |
| Unique warning message groups | 47 |

Error counts by compiler diagnostic:

| Code | Count | Meaning in this pass |
| --- | ---: | --- |
| CS1069 | 2,580 | Forwarded type needs a non-framework assembly/package |
| CS0103 | 2,269 | Mostly missing original build-input `SR`, `AssemblyRef`, and `ModName` symbols |
| CS0246 | 684 | Missing type, sibling assembly, platform API, or generated input |
| CS0182 | 110 | Cascades from missing constant-valued generated symbols |
| CS0234 | 57 | Missing namespace/assembly partition |
| CS0616 | 28 | Cascades from unavailable attribute types |
| CS0118 | 23 | Cascades from unavailable configuration types |
| CS0115 | 19 | Cascades from unavailable base types or removed remoting members |
| CS0538 | 16 | Cascades from unavailable interfaces |
| CS1503 | 6 | Overload binding after framework surface changes |
| CS0641 | 1 | Cascade from an unavailable attribute base |
| SYSLIB0011 | 1 | Formatter serialization is blocked/obsolete |

## Deduplicated categories

The following categories are mutually exclusive attribution buckets for all
5,794 error occurrences. The classification is deterministic and reviewable in
`eng/analyze-build-diagnostics.py`; it does not claim that each occurrence is
an independent root cause.

| Category | Errors | Share | Main dependency/root cause | Resolution character |
| --- | ---: | ---: | --- | --- |
| Source, preprocessing, and resource generation | 2,193 | 37.8% | Missing `SR`, `AssemblyRef`, `ModName`, `.cspp` outputs, cache structures, and regex/resource generation | Mechanical first, with byte-for-byte validation |
| Configuration dependencies | 2,162 | 37.3% | 2,058 forwarded references to `System.Configuration.ConfigurationManager`, plus resulting missing validators and configuration interfaces | Package reference is mechanical; hierarchy and reload semantics are architectural |
| AppDomain, remoting, CAS, and serialization | 412 | 7.1% | CAS types/attributes, remoting messaging/object handles, lifetime services, formatter serialization | Primarily behavioral/architectural |
| IIS, Windows, native, and design-time dependencies | 337 | 5.8% | Directory Services, Enterprise Services, design editors/drawing, COM/host interfaces, Windows-only framework partitions | Profile and platform decisions required |
| CodeDOM and runtime compilation | 308 | 5.3% | 279 forwarded `System.CodeDom` types and 29 MSBuild/compiler-host dependencies | References are mechanical; compiler-provider behavior is architectural |
| Secondary/cascading compiler diagnostics | 203 | 3.5% | Missing constants, attributes, interfaces, and base types | Recount after prerequisites; do not fix individually |
| Other missing assemblies or packages | 178 | 3.1% | `System.Data.SqlClient`, application-services membership types, `System.Runtime.Caching`, `System.Web.Services`, and data protection | Mostly mechanical compile surface, followed by behavioral validation |
| Removed or inaccessible BCL/framework APIs not covered above | 1 | <0.1% | CLR-internal `ICustomLoaderHelperFunctions` | Architectural replacement or explicit unsupported path |

### 1. Source, preprocessing, and resource generation

This is one small set of absent build products repeated throughout the source,
not 2,193 unrelated porting defects.

| Missing product | Direct errors | Evidence | Affected areas | Likely strategy |
| --- | ---: | --- | --- | --- |
| `SR` | 1,667 | `CS0103` throughout UI, configuration, compilation, and runtime | Nearly all subsystems | Recreate the original `System.Web.txt` resource-generation pipeline and strongly typed resource constants; validate names and resource bytes |
| `AssemblyRef` | 300 | `CS0103` in attributes and type-name constants | UI, configuration, compilation | Generate the original assembly-name constants from authoritative framework identity data |
| `ModName` | 212 | `CS0103` in native declarations | Hosting, runtime, state, native methods | Preprocess `Names.cspp` with explicit target definitions; do not choose `FEATURE_PAL` silently |
| Cache `.cspp` outputs | 6 | Missing `CacheUsage`, `CacheExpires`, `UsageEntryRef`, `ExpiresEntryRef` | Cache | Recreate preprocessing of `cacheusage.cspp` and `cacheexpires.cspp` |
| Regular-expression/resource partitions | 8 | Missing `System.Web.RegularExpressions`, DataAnnotations resource namespace, and resource tools | Configuration, validation | Determine original generated assembly/source boundary and reproduce it explicitly |

Representative errors:

```text
Cache/CacheEntry.cs(111,9) CS0246: ExpiresEntryRef could not be found
UI/WebControls/MenuItemBinding.cs(227,27) CS0103: SR does not exist
NativeMethods.cs(...) CS0103: ModName does not exist
```

These are mechanical only if generated outputs match the original inputs and
macro choices. Selecting different native module names or resource identities
would be a compatibility decision.

### 2. Configuration dependencies

The compiler explicitly forwards 2,058 occurrences to
`System.Configuration.ConfigurationManager`. A package/reference can restore
the compile-time types. That does not answer how machine-level configuration,
root `web.config`, application inheritance, protected sections, file watching,
restart, remote configuration, and IIS metabase behavior will work.

Affected subsystems extend beyond `Configuration`: cache and provider types,
membership/profile, compilation, session state, and hosting all depend on the
classic provider/configuration model.

Likely strategy:

1. Add the compatible configuration reference in a dedicated mechanical
   change and rerun the inventory.
2. Preserve public configuration types and parsing where feasible.
3. Put machine/IIS configuration discovery, inheritance, reload, and protected
   configuration behind explicit services and ADRs.
4. Validate configuration behavior against .NET Framework before declaring
   the category resolved.

### 3. AppDomain, remoting, CAS, and serialization

The direct compiler errors understate this category. The build also emits 692
`SYSLIB0003` warnings stating that Code Access Security is not supported or
honored, 33 obsolete override warnings involving remoting lifetime services or
formatter serialization, 21 `SYSLIB0004` warnings for unsupported constrained
execution regions, and a `SYSLIB0011` error for formatter serialization.

Clean-source scans find AppDomain/remoting/`MarshalByRefObject` concepts in 64
files (202 matches) and formatter/serialization concepts in 23 files (87
matches). These mechanisms participate in application isolation, unload,
shadow copying, remote configuration, build-manager hosting, exception/state
serialization, and cache/session state.

Adding `System.Security.Permissions` may remove 165 forwarded-type errors and
some related missing symbols, but it cannot restore CAS enforcement. Treating
those APIs as harmless attributes or no-ops would be a semantic decision, not
a mechanical fix.

Required decisions include:

- process or `AssemblyLoadContext` boundaries for application isolation and
  reload;
- replacement for transparent remoting proxies and lifetime services;
- fail/ignore/emulate policy for CAS declarations and demands;
- allowed serialization formats and migration/security policy;
- request termination and timeout semantics. The source still calls
  `Thread.CurrentThread.Abort(...)` and catches `ThreadAbortException` across
  the pipeline and page lifecycle, although that later-wave incompatibility is
  not yet in the reported error set.

### 4. IIS, Windows, native, and design-time dependencies

The largest direct groups are 164 missing `UITypeEditor` references and at
least 127 Directory Services permission/type references. Other errors involve Enterprise
Services transactions, drawing/toolbox attributes, COM export and hosting
interfaces, data designers, and Windows configuration namespaces.

The clean source contains 381 `DllImport`/`ComImport` declarations across 30
files. `ModName` references include IIS engine/worker-process modules,
`kernel32`, `advapi32`, `ole32`, the classic CLR, and the ASP.NET state service.
Most P/Invoke declarations compile once constants exist, so their runtime
incompatibility is not represented by the 337-error count.

Likely strategy:

- separate runtime-critical hosting APIs from optional design-time metadata;
- define supported cross-platform and Windows-specific profiles before
  excluding or replacing code;
- route host and file-monitoring operations through a host-neutral boundary;
- preserve explicit failures for unsupported impersonation, COM+, Active
  Directory, IIS integrated-pipeline, and state-service paths;
- do not make silent no-ops.

### 5. CodeDOM and runtime compilation

The build reports 279 forwarded references to `System.CodeDom`; the remaining
29 errors are mainly Microsoft.Build types and compiler-host event contracts.
Eighty-five clean-source files directly mention CodeDOM/provider/compiler
concepts.

Adding references is a mechanical first step. Runtime compilation is not: the
project must preserve configured language/provider selection, C# and Visual
Basic behavior, compiler options, generated source and line mappings,
references, error reporting, batch compilation, and application reload.

The safest direction is to retain parser and CodeDOM generation contracts while
introducing a separately tested compilation service. The service architecture
and provider selection require an ADR and differential fixtures before code is
changed.

### 6. Other missing assemblies or packages

The compiler identifies these forwarded assembly counts:

| Assembly | Forwarded errors |
| --- | ---: |
| `System.Configuration.ConfigurationManager` | 2,058 |
| `System.CodeDom` | 279 |
| `System.Security.Permissions` | 165 |
| `System.Data.SqlClient` | 77 |
| `System.Drawing.Common` | 1 |

Additional non-forwarded groups include membership and role types normally
partitioned into `System.Web.ApplicationServices`, `System.Runtime.Caching`,
`System.Web.Services`, design assemblies, Directory Services, Enterprise
Services, MSBuild, and generated `System.Web.RegularExpressions`.

Each dependency needs one of four explicit dispositions: runtime dependency,
build-only dependency, separately ported sibling component, or unsupported
profile. Package availability alone is not sufficient evidence that behavior
is compatible or cross-platform.

### 7. Secondary diagnostics

The 203 secondary errors include 110 invalid attribute arguments caused mainly
by missing constant-valued `ModName`/`AssemblyRef`, 28 unavailable attribute
classes, 23 configuration namespace/type collisions, 19 missing overrides, 16
invalid explicit-interface declarations, and 7 other binding errors.

They must be recounted after their prerequisite types and generated inputs are
restored. Fixing them line by line now would obscure root causes and risk source
divergence.

## Affected subsystems

| Subsystem | Errors | Primary pressure |
| --- | ---: | --- |
| Configuration | 2,243 | ConfigurationManager types, CAS, generated resources |
| UI | 2,223 | `SR`, `AssemblyRef`, design-time types, CAS attributes |
| Root runtime files | 330 | `ModName`, CAS, remoting, application services |
| Compilation | 329 | CodeDOM, MSBuild, resources, remoting |
| Security | 228 | CAS, Directory Services, membership, serialization |
| Hosting | 183 | AppDomain/remoting, native/IIS interfaces, runtime caching |
| Management | 57 | Configuration, Windows eventing/monitoring, provider bases |
| State | 47 | SQL client, native state service, serialization |
| Util | 47 | CAS, Enterprise Services, native/runtime helpers |
| Cache | 45 | Generated cache sources, configuration, SQL client |
| Other | 62 | Profile, properties, data access, WebSockets, handlers, globalization |

## Mechanical work versus decisions

### Mechanical, if performed in isolated commits and verified

- reproduce `System.Web.txt`, `Names.cspp`, `cacheusage.cspp`, and
  `cacheexpires.cspp` build products;
- generate `SR`, `AssemblyRef`, and other original build constants without
  editing imported sources;
- add compile-time references for forwarded assemblies one dependency at a
  time and rerun the inventory after each coherent layer;
- restore correct sibling-assembly/source partitions such as application
  services and generated regular expressions;
- separate design-time-only inputs from runtime inputs based on the original
  build boundary, not by arbitrary error deletion;
- maintain deterministic build metadata and diagnostic extraction.

### Behavioral or architectural decisions

- AppDomain isolation, unload/reload, shadow copy, and static-state lifetime;
- remoting proxies, callback/lifetime behavior, and remote configuration;
- CAS declarations/demands and unsupported security behavior;
- formatter-based serialization and persisted legacy payload compatibility;
- `Thread.Abort`, `Response.End`, redirects, timeouts, and lifecycle unwinding;
- IIS integrated-pipeline/native host services, file monitoring, process model,
  and performance counters;
- Windows identity, impersonation, COM+, Active Directory, registry, and
  platform support profiles;
- runtime compiler/provider selection, including Visual Basic and custom
  providers;
- configuration hierarchy, protected configuration, reload, and restart;
- SQL client/provider identity and legacy configuration compatibility;
- any source exclusion, no-op, emulation, or unsupported-feature behavior.

## Safest resolution order

1. **Recreate original generated build inputs.** This removes the largest
   amplification source without changing runtime behavior and makes later
   diagnostics trustworthy.
2. **Restore foundational compile-time references.** Add
   ConfigurationManager, CodeDOM, and other uncontroversial build/runtime
   references in small audited layers; rebuild after each layer.
3. **Restore original assembly partitions.** Bring in or separately scaffold
   application services, regular expressions, resources, and other sibling
   contracts before rewriting callers.
4. **Separate runtime, design-time, and platform profiles.** Decide what must be
   in the cross-platform runtime build versus Windows-specific or design-time
   builds; document exclusions before applying them.
5. **Characterize configuration and runtime compilation.** Build differential
   fixtures and ADRs before replacing their hosting mechanisms.
6. **Decide AppDomain/remoting/request-termination architecture.** These choices
   affect application lifetime and the entire request pipeline and should
   precede local fixes in hosting, compilation, and configuration.
7. **Decide security and serialization policy.** CAS, formatter serialization,
   machine keys, authentication, and legacy payloads require explicit threat
   modeling and compatibility switches where defensible.
8. **Implement native/IIS/Windows adapters last among compile categories.** By
   then the host-neutral contracts and platform profiles should constrain the
   replacements and prevent native assumptions from spreading.
9. **Run the next clean inventory.** Only after these layers should remaining
   method-body/API errors be classified and candidate POC patches evaluated.

This order deliberately removes high-fan-out mechanical noise before making
semantic choices, while forcing architecture and security decisions before any
temporary no-op can become an accidental contract.
