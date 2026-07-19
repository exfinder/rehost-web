# .NET 10 inventory after Directory Services references

## Scope

This is the sixth foundational dependency pass. It adds centrally managed
direct runtime references to `System.DirectoryServices` and
`System.DirectoryServices.Protocols`, both 10.0.10. Neither resolves additional
package dependencies for `net10.0`.

The two packages form one group because both are used by the same Active
Directory membership implementation. No Reference Source, directory behavior,
shim, source inclusion, feature constant, or warning policy changed.

## Diagnostic delta

| Measure | Drawing.Common pass | Directory Services pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 435 | 392 | **-43** |
| Warnings | 1,006 | 1,006 | 0 |
| All diagnostics | 1,441 | 1,398 | **-43** |
| Files with errors | 120 | 119 | -1 |
| Files with warnings | 167 | 167 | 0 |
| Unique message groups | 121 | 112 | -9 |

All 43 removals are in the IIS/Windows/native/design-time category. Every other
category and every warning count remained unchanged.

The remaining 392 errors comprise 177 IIS/Windows/native/design-time, 162
other package/sibling-assembly, 29 Microsoft.Build/compiler-host, 19
AppDomain/remoting/CAS/serialization, two generated/sibling-partition, and one
each residual configuration, cascade, and inaccessible CLR-internal error.

This pass establishes directory type availability only. ADSI/Active Directory
platform support, LDAP native dependencies, authentication, impersonation, and
Unix feature gaps remain deferred decisions.

Build: SDK 10.0.302 / MSBuild 18.6.11.33009, macOS arm64, exit 1, 3.14 seconds.
Only the normalized summary is retained; temporary detailed artifacts are not.
