# .NET 10 inventory after Runtime.Caching reference

## Scope

This is the fourth foundational package-reference pass. It adds only a
centrally managed direct runtime reference to `System.Runtime.Caching` 10.0.10.
Its resolved dependency, `System.Configuration.ConfigurationManager` 10.0.10,
was already direct.

No Reference Source, cache behavior, shim, source inclusion, feature constant,
or warning policy changed.

## Diagnostic delta

| Measure | Security.Permissions pass | Runtime.Caching pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 473 | 450 | **-23** |
| Warnings | 1,006 | 1,006 | 0 |
| All diagnostics | 1,479 | 1,456 | **-23** |
| Files with errors | 122 | 121 | -1 |
| Files with warnings | 167 | 167 | 0 |
| Unique message groups | 135 | 126 | -9 |

The missing-package category fell by 16, IIS/Windows/native/design-time by two,
and cascades by five. All other categories and warnings remained unchanged.

The remaining 450 errors comprise 235 IIS/Windows/native/design-time, 162
other package/sibling-assembly, 29 Microsoft.Build/compiler-host, 19
AppDomain/remoting/CAS/serialization, two generated/sibling-partition, and one
each residual configuration, cascade, and inaccessible CLR-internal error.

This pass establishes cache type availability only. Cache pressure thresholds,
physical-memory reporting, file monitoring, and cross-platform behavior remain
deferred decisions.

Build: SDK 10.0.302 / MSBuild 18.6.11.33009, macOS arm64, exit 1, 2.34 seconds.
Only the normalized summary is retained; temporary detailed artifacts are not.
