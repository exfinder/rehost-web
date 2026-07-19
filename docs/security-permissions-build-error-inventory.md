# .NET 10 inventory after Security.Permissions reference

## Scope

This is the third foundational package-reference pass. It adds only a centrally
managed direct runtime reference to `System.Security.Permissions` 10.0.10. Its
resolved `net10.0` dependency is `System.Windows.Extensions` 10.0.10.

This pass made no changes under `src/System.Web.ReferenceSource`, added no CAS
emulation or compatibility shim, excluded no sources, changed no feature
constants, and suppressed no warnings.

## Diagnostic delta

| Measure | CodeDOM pass | Security.Permissions pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 1,003 | 473 | **-530** |
| Warnings | 757 | 1,006 | **+249** |
| All diagnostics | 1,760 | 1,479 | **-281** |
| Files with errors | 185 | 122 | -63 |
| Files with warnings | 163 | 167 | +4 |
| Unique message groups | 154 | 135 | -19 |

All 165 forwarded-reference errors naming `System.Security.Permissions`
disappeared. Restoring the permission types also removed 365 dependent errors:

| Category | Before | After | Delta |
| --- | ---: | ---: | ---: |
| AppDomain, remoting, CAS, and serialization | 412 | 19 | **-393** |
| IIS, Windows, native, and design-time dependencies | 337 | 237 | **-100** |
| Secondary/cascading compiler diagnostics | 33 | 6 | **-27** |
| Configuration dependencies | 11 | 1 | **-10** |
| All other categories | 210 | 210 | 0 |

Warnings increased because previously missing permission types now bind: 248
additional `SYSLIB0003` CAS-obsolescence warnings and one additional `CS0672`
obsolete-override warning became visible. No warning was suppressed.

## Remaining blockers

The remaining 473 errors comprise:

- 237 IIS/Windows/native/design-time errors;
- 178 other package or sibling-assembly errors;
- 29 Microsoft.Build/compiler-host errors;
- 19 remaining AppDomain/remoting/CAS/serialization errors;
- six cascades;
- two generated/sibling-partition errors;
- one residual configuration error;
- one inaccessible CLR-internal API error.

This pass restores legacy CAS type availability only. .NET 10 does not enforce
Code Access Security. Demand behavior, declarations, sandboxing, and unsupported
security behavior remain deferred architectural/security decisions.

The build used SDK 10.0.302 / MSBuild 18.6.11.33009 on macOS arm64 and exited
with code 1 after 3.88 seconds. The analyzer produced 1,479 normalized
diagnostics. Only its summary is retained; temporary logs, binlogs, and detailed
inventories are not retained.
