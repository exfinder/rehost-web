# .NET 10 inventory after deterministic build-input generation

## Scope and guardrails

This is the second `net10.0` diagnostic inventory. It follows the clean
5,794-error / 755-warning baseline and adds only the deterministic original
build-input generation described in `docs/generated-build-inputs.md`.

This pass made no changes under `src/System.Web.ReferenceSource`, added no
package references, compatibility shims, runtime implementations, source
exclusions, feature constants, or warning suppressions. Generated sources and
the neutral resource are emitted under the runtime project's `obj` directory.

## Reproduction

| Item | Value |
| --- | --- |
| Command | `dotnet build src/Rehost.WebForms.Runtime/Rehost.WebForms.Runtime.csproj --nologo --verbosity:minimal -flp:logfile=artifacts/build/net10.0-generated-inputs/build.log;verbosity=diagnostic -bl:artifacts/build/net10.0-generated-inputs/build.binlog` |
| SDK | 10.0.302 |
| MSBuild | 18.6.11.33009 |
| Host runtime | 10.0.10 |
| Host | macOS 26.5, arm64 |
| Exit code | 1 |
| Elapsed | 23.24 seconds |

Raw log and binlog hashes are recorded in
`artifacts/build/net10.0-generated-inputs/build-metadata.json`. The normalized
inventory contains all 4,242 diagnostics exactly once.

The inventory was refreshed after matching the POC ResGen resource binary. Its
diagnostic counts and normalized inventory SHA-256 remained identical.

## Headline delta

| Measure | Clean baseline | Generated-input pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 5,794 | 3,487 | **-2,307** |
| Warnings | 755 | 755 | 0 |
| All diagnostics | 6,549 | 4,242 | **-2,307** |
| Files with errors | 491 | 354 | -137 |
| Files with warnings | 163 | 163 | 0 |
| Unique message groups | 288 | 277 | -11 |

The warning count and every non-generation root-cause category remained
unchanged. This is the expected boundary for a mechanical generation-only
layer.

## Diagnostic-code delta

| Code | Before | After | Delta | Explanation |
| --- | ---: | ---: | ---: | --- |
| CS0103 | 2,269 | 90 | -2,179 | Generated `SR`/`ModName` and static `AssemblyRef` symbols plus related constants now bind |
| CS0182 | 110 | 0 | -110 | Attribute arguments became compile-time constants after `AssemblyRef`/`ModName` generation |
| CS0234 | 57 | 51 | -6 | `System.Web.RegularExpressions` namespace/types restored |
| CS0246 | 684 | 678 | -6 | Cache `.cspp` types restored |
| CS1503 | 6 | 0 | -6 | Cascading overload failures disappeared after generated prerequisites bound |
| All other error codes | 2,708 | 2,708 | 0 | Outside this mechanical layer |

Direct missing-symbol groups are fully eliminated:

| Restored build-product marker | Before | After |
| --- | ---: | ---: |
| `SR` | 1,667 | 0 |
| `AssemblyRef` | 300 | 0 |
| `ModName` | 212 | 0 |
| Cache generated symbols | 6 | 0 |
| `System.Web.RegularExpressions` | 6 | 0 |

These direct groups account for 2,191 errors. A further 116 secondary errors
disappeared once the generated constants and types were present, producing the
total reduction of 2,307.

## Category delta

| Category | Before | After | Delta |
| --- | ---: | ---: | ---: |
| Source, preprocessing, and resource generation | 2,193 | 2 | **-2,191** |
| Secondary/cascading compiler diagnostics | 203 | 87 | **-116** |
| Configuration dependencies | 2,162 | 2,162 | 0 |
| AppDomain, remoting, CAS, and serialization | 412 | 412 | 0 |
| IIS, Windows, native, and design-time dependencies | 337 | 337 | 0 |
| CodeDOM and runtime compilation | 308 | 308 | 0 |
| Other missing assemblies or packages | 178 | 178 | 0 |
| Removed or inaccessible BCL/framework APIs | 1 | 1 | 0 |

The two remaining diagnostics classified as source/resource generation are
different assembly partitions and were intentionally not absorbed into this
layer:

- `System.Resources.Tools` in `BaseResourcesBuildProvider.cs`;
- `System.ComponentModel.DataAnnotations.Resources` in
  `DataAnnotations/LocalizableString.cs`.

Resolving them requires the later sibling/build-tool assembly-partition pass,
not changes to the generated inputs covered here.

## Unchanged blockers

The remaining 3,487 errors are dominated by the same unresolved layers as the
clean inventory:

- 2,162 configuration dependency errors, including 2,058 forwarded references
  to `System.Configuration.ConfigurationManager`;
- 412 AppDomain/remoting/CAS/serialization errors;
- 337 IIS, Windows, native, and design-time dependency errors;
- 308 CodeDOM/runtime-compilation dependency errors;
- 178 other package or sibling-assembly errors;
- 87 remaining cascades and one inaccessible CLR-internal API error.

The forwarded-assembly counts are exactly unchanged: ConfigurationManager
2,058, CodeDOM 279, Security.Permissions 165, SqlClient 77, and Drawing.Common
1. No package layer has started.

## Values deferred to explicit decisions

- Public assembly and package identity beyond the provisional unsigned
  `System.Web, Version=4.0.0.0` output.
- Strong-name and binary-compatibility policy; the Microsoft private key cannot
  be reproduced.
- Whether original Microsoft-qualified reflection/configuration strings should
  remain literal, be redirected, or fail explicitly in each compatibility
  profile.
- Modern mapping for `System.Configuration`, `System.Design`,
  `System.Web.Mobile`, and `System.Web.DynamicData` assembly references.
- Cross-platform replacements for the explicitly selected desktop Windows
  native module names.
- The sibling boundary for `System.Web.RegularExpressions`; this pass preserves
  its original constructible types in generated source within the provisional
  runtime build, without making a package/assembly architecture decision.
