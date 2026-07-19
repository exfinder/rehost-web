# .NET 10 inventory after ConfigurationManager reference

## Scope

This is the first foundational package-reference pass after deterministic
build-input generation. It adds only a centrally managed direct runtime
reference to `System.Configuration.ConfigurationManager` 10.0.10.

The resolved package graph also contains its 10.0.10 dependencies:
`System.Diagnostics.EventLog` and
`System.Security.Cryptography.ProtectedData`. No package API is called by new
code in this pass.

This pass made no changes under `src/System.Web.ReferenceSource`, added no
compatibility shims or runtime implementations, excluded no sources, changed
no feature constants, and suppressed no warnings.

## Reproduction

| Item | Value |
| --- | --- |
| Command | `dotnet build src/Rehost.WebForms.Runtime/Rehost.WebForms.Runtime.csproj --nologo --verbosity:minimal '-flp:logfile=artifacts/build/net10.0-configuration-manager/build.log;verbosity=diagnostic' -bl:artifacts/build/net10.0-configuration-manager/build.binlog` |
| SDK | 10.0.302 |
| MSBuild | 18.6.11.33009 |
| Host runtime | 10.0.10 |
| Host | macOS 26.5, arm64 |
| Exit code | 1 |
| Elapsed | 3.46 seconds |

The analyzer produced 2,039 normalized diagnostics. Only its summary is
retained for this intermediate dependency group; raw logs, binlogs, and
detailed TSV inventories are not retained.

## Headline delta

| Measure | Generated-input pass | ConfigurationManager pass | Delta |
| --- | ---: | ---: | ---: |
| Errors | 3,487 | 1,282 | **-2,205** |
| Warnings | 755 | 757 | **+2** |
| All diagnostics | 4,242 | 2,039 | **-2,203** |
| Files with errors | 354 | 218 | -136 |
| Files with warnings | 163 | 163 | 0 |
| Unique message groups | 277 | 182 | -95 |

All 2,058 forwarded-reference errors naming
`System.Configuration.ConfigurationManager` disappeared. Configuration-category
errors fell by 2,151 and cascades fell by 54, producing the net reduction of
2,205.

## Category delta

| Category | Before | After | Delta |
| --- | ---: | ---: | ---: |
| Configuration dependencies | 2,162 | 11 | **-2,151** |
| Secondary/cascading compiler diagnostics | 87 | 33 | **-54** |
| AppDomain, remoting, CAS, and serialization | 412 | 412 | 0 |
| IIS, Windows, native, and design-time dependencies | 337 | 337 | 0 |
| CodeDOM and runtime compilation | 308 | 308 | 0 |
| Other missing assemblies or packages | 178 | 178 | 0 |
| Source, preprocessing, and resource generation | 2 | 2 | 0 |
| Removed or inaccessible BCL/framework APIs | 1 | 1 | 0 |

The 11 remaining configuration-category errors are ten
`ConfigurationPermission`/`ConfigurationPermissionAttribute` references
forwarded to `System.Security.Permissions` and one missing
`System.Net.Configuration` namespace. They belong to later dependency/profile
groups.

## Warning delta

Two `CS0672` warnings became visible because configuration base types now
bind. They report obsolete `GetRestrictedPermissions` overrides in
`RemoteWebConfigurationHost` and `WebConfigurationHost`. No warning was
suppressed or changed.

## Remaining blockers

The remaining 1,282 errors are dominated by unchanged dependency groups:

- 412 AppDomain/remoting/CAS/serialization errors, including 165 forwarded
  references to `System.Security.Permissions`;
- 337 IIS/Windows/native/design-time errors;
- 308 CodeDOM/runtime-compilation errors, including 279 forwarded references
  to `System.CodeDom`;
- 178 other package or sibling-assembly errors, including 77 forwarded
  references to `System.Data.SqlClient`;
- 33 cascades, 11 residual configuration errors, two generated/sibling
  partition errors, and one inaccessible CLR-internal API error.

This pass establishes compile-time availability only. Configuration hierarchy,
reload, protected sections, remote configuration, IIS integration, and
cross-platform behavior remain deferred architectural decisions.
