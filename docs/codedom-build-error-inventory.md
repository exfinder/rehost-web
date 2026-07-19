# .NET 10 inventory after CodeDOM reference

## Scope

This is the second foundational package-reference pass. It adds only a
centrally managed direct runtime reference to `System.CodeDom` 10.0.10. The
package has no resolved transitive package dependencies for `net10.0`.

This pass made no changes under `src/System.Web.ReferenceSource`, added no
compiler implementation or compatibility shim, excluded no sources, changed
no feature constants, and suppressed no warnings.

## Reproduction

| Item | Value |
| --- | --- |
| Command | `dotnet build src/Rehost.WebForms.Runtime/Rehost.WebForms.Runtime.csproj --nologo --verbosity:minimal` (diagnostic file/binlog options used temporarily) |
| SDK | 10.0.302 |
| MSBuild | 18.6.11.33009 |
| Host runtime | 10.0.10 |
| Host | macOS 26.5, arm64 |
| Exit code | 1 |
| Elapsed | 2.09 seconds |

The analyzer produced 1,760 normalized diagnostics. Only its summary is
retained for this intermediate dependency group; raw logs, binlogs, and
detailed TSV inventories are not retained.

## Diagnostic delta

| Measure | ConfigurationManager pass | CodeDOM pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 1,282 | 1,003 | **-279** |
| Warnings | 757 | 757 | 0 |
| All diagnostics | 2,039 | 1,760 | **-279** |
| Files with errors | 218 | 185 | -33 |
| Files with warnings | 163 | 163 | 0 |
| Unique message groups | 182 | 154 | -28 |

All 279 forwarded-reference errors naming `System.CodeDom` disappeared. The
CodeDOM/runtime-compilation category fell from 308 to 29; every other category
and every warning count remained unchanged.

The remaining 29 category errors are Microsoft.Build compiler-host contracts,
including build event arguments, `IBuildEngine`, task classes, and target
framework helpers. They require a separate build-tool dependency-boundary and
version decision.

## Remaining blockers

The remaining 1,003 errors comprise:

- 412 AppDomain/remoting/CAS/serialization errors;
- 337 IIS/Windows/native/design-time errors;
- 178 other package or sibling-assembly errors;
- 33 cascades;
- 29 Microsoft.Build/compiler-host errors;
- 11 residual configuration errors;
- two generated/sibling-partition errors;
- one inaccessible CLR-internal API error.

This pass establishes CodeDOM compile/runtime type availability only. Language
provider selection, C#/Visual Basic compilation, compiler options, generated
source, and error behavior remain deferred architectural decisions.
