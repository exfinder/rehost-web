# .NET 10 inventory after MSBuild Tasks reference

## Scope

This is the eighth foundational dependency pass. It adds a centrally managed
direct runtime reference to `Microsoft.Build.Tasks.Core` 18.8.2. The package
supplies the task, framework, and utilities assemblies used directly by the
Reference Source runtime-compilation path.

No Reference Source, compiler behavior, shim, source inclusion, feature
constant, or warning policy changed.

## Diagnostic delta

| Measure | SqlClient pass | MSBuild Tasks pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 315 | 286 | **-29** |
| Source warnings | 1,083 | 1,083 | 0 |
| Normalized source diagnostics | 1,398 | 1,369 | **-29** |
| Files with errors | 112 | 107 | -5 |
| Files with source warnings | 173 | 173 | 0 |
| Unique source message groups | 112 | 105 | -7 |

All 29 Microsoft.Build/compiler-host errors disappeared. Every remaining error
category and source warning count remained unchanged.

## Dependency-audit finding

`Microsoft.Build.Tasks.Core` 18.8.2 pins several `System.*` dependencies at
10.0.4. Its graph therefore resolved `System.Security.Cryptography.Xml` 10.0.4,
which produced two distinct high-severity `NU1903` advisories during restore
and again during build (four output occurrences). These build warnings are
outside the normalized source diagnostic counts above.

The patched 10.0 line begins at 10.0.6; the current serviced package is 10.0.10.
The subsequent direct security-pin pass resolves 10.0.10 and removes both
advisories; see `docs/cryptography-xml-security-pin-build-error-inventory.md`.

## Remaining blockers

The remaining 286 errors comprise 177 IIS/Windows/native/design-time, 85 other
package/sibling-assembly, 19 AppDomain/remoting/CAS/serialization, two
generated/sibling-partition, and one each residual configuration, cascade, and
inaccessible CLR-internal error.

This pass establishes MSBuild task type availability only. Task execution,
toolset selection, compiler/provider behavior, target-framework resolution,
and deployment remain deferred decisions.

Build: SDK 10.0.302 / MSBuild 18.6.11.33009, macOS arm64, exit 1, 6.17 seconds.
Only the normalized summary is retained; temporary detailed artifacts are not.
