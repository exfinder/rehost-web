# .NET 10 inventory after design-time metadata marker

## Scope and decision

This pass adds an internal `System.Drawing.Design.UITypeEditor` compile-time
marker to the runtime project. It is a narrowly approved compatibility-shim
exception for legacy `EditorAttribute` metadata. Windows and design-time
functionality are unsupported; the marker is non-instantiable and implements
no behavior.

The marker remains outside Reference Source. A separate assembly was rejected:
it would require a public placeholder and add a false design-time dependency.
In .NET Framework, `UITypeEditor` belonged to `System.Drawing.dll`, which cannot
be reproduced as a new assembly without conflicting with modern framework
assemblies.

The runtime project now uses normal SDK discovery for its local source files;
the external Reference Source glob remains explicit. A second build after this
project simplification produced a byte-identical normalized diagnostic list.

## Diagnostic delta

| Measure | Security-pin pass | Marker pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 286 | 122 | **-164** |
| Warnings | 1,083 | 1,083 | 0 |
| All diagnostics | 1,369 | 1,205 | **-164** |
| Files with errors | 107 | 47 | -60 |
| Unique message groups | 105 | 104 | -1 |

All 164 removed errors were missing `UITypeEditor` references. Remaining errors:
85 missing sibling/package types, 19 AppDomain/remoting/serialization, 13 other
Windows/native/design-time, two source/resource partition, and one each
configuration, inaccessible framework API, and cascade.

No Reference Source, source exclusion, warning policy, or runtime behavior
changed. Build: SDK 10.0.302 / MSBuild 18.6.11.33009, macOS arm64, exit 1,
4.33 seconds. Only the normalized summary is retained; temporary detailed
artifacts are not.
