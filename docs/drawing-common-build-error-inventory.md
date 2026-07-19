# .NET 10 inventory after Drawing.Common reference

## Scope

This is the fifth foundational package-reference pass. It adds only a centrally
managed direct runtime reference to `System.Drawing.Common` 10.0.10. Its
resolved dependency is `Microsoft.Win32.SystemEvents` 10.0.10.

No Reference Source, drawing behavior, shim, source inclusion, feature
constant, or warning policy changed.

## Diagnostic delta

| Measure | Runtime.Caching pass | Drawing.Common pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 450 | 435 | **-15** |
| Warnings | 1,006 | 1,006 | 0 |
| All diagnostics | 1,456 | 1,441 | **-15** |
| Files with errors | 121 | 120 | -1 |
| Files with warnings | 167 | 167 | 0 |
| Unique message groups | 126 | 121 | -5 |

All 15 removals are in the IIS/Windows/native/design-time category, including
the sole forwarded `System.Drawing.Common` diagnostic. Contrary to the initial
dependency estimate, `UITypeEditor` remains unavailable: the package does not
restore the classic design-time surface.

The remaining 435 errors comprise 220 IIS/Windows/native/design-time, 162
other package/sibling-assembly, 29 Microsoft.Build/compiler-host, 19
AppDomain/remoting/CAS/serialization, two generated/sibling-partition, and one
each residual configuration, cascade, and inaccessible CLR-internal error.

This pass establishes available drawing type/attribute metadata only.
`System.Drawing.Common` is Windows-only for runtime drawing operations on
.NET 10; no cross-platform support claim is made.

Build: SDK 10.0.302 / MSBuild 18.6.11.33009, macOS arm64, exit 1, 3.31 seconds.
Only the normalized summary is retained; temporary detailed artifacts are not.
