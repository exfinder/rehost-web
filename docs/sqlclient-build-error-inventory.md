# .NET 10 inventory after System.Data.SqlClient reference

## Scope

This is the seventh foundational dependency pass. It adds a centrally managed
direct runtime reference to `System.Data.SqlClient` 4.9.1, preserving the
Reference Source namespace, provider name, and public concrete SQL types. Its
resolved package dependency is `runtime.native.System.Data.SqlClient.sni`
4.4.0.

No Reference Source, SQL behavior, provider substitution, shim, source
inclusion, feature constant, or warning policy changed.

## Diagnostic delta

| Measure | Directory Services pass | SqlClient pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 392 | 315 | **-77** |
| Warnings | 1,006 | 1,083 | **+77** |
| All diagnostics | 1,398 | 1,398 | 0 |
| Files with errors | 119 | 112 | -7 |
| Files with warnings | 167 | 173 | +6 |
| Unique message groups | 112 | 112 | 0 |

All 77 forwarded `System.Data.SqlClient` errors disappeared. They became 77
`CS0618` warnings because the restored legacy types are obsolete and direct
users to `Microsoft.Data.SqlClient`. No warning was suppressed.

The remaining 315 errors comprise 177 IIS/Windows/native/design-time, 85 other
package/sibling-assembly, 29 Microsoft.Build/compiler-host, 19
AppDomain/remoting/CAS/serialization, two generated/sibling-partition, and one
each residual configuration, cascade, and inaccessible CLR-internal error.

This pass preserves existing source/public API compatibility. The legacy and
modern SQL providers can coexist, but their concrete types are not
interchangeable. Provider modernization remains a later compatibility/API
decision.

- [ ] **TODO — SQL provider:** Evaluate a dual-provider strategy, adapters, or
  additive overloads for `Microsoft.Data.SqlClient` while preserving legacy
  public types. Cover provider configuration names, SQL cache dependency,
  membership/profile providers, session state, and public exception/command
  types. Require an ADR and differential tests before changing provider
  identity or behavior.

Build: SDK 10.0.302 / MSBuild 18.6.11.33009, macOS arm64, exit 1, 3.79 seconds.
Only the normalized summary is retained; temporary detailed artifacts are not.
