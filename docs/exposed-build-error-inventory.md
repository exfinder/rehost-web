# Newly exposed build-error inventory

## Checkpoint

This inventory records the compiler wave exposed after the XSD build-provider
declaration errors were removed on July 20, 2026. It uses the mandated
non-hanging Runtime build command with an additional diagnostic file logger.

| Measure | Count |
| --- | ---: |
| Errors | 79 |
| Warnings | 2,441 |
| Files with errors | 23 |
| Files with warnings | 249 |

MSBuild logged each diagnostic twice (task output and summary). The inventory
analyzer now deduplicates the full diagnostic identity; raw reported totals and
all tables below count each source diagnostic once.

## Error groups

Groups are mutually exclusive, assigned by affected file and API family.

| Group | Errors | Main locations / missing contract |
| --- | ---: | --- |
| AppDomain, remoting, dynamic assembly | 31 | `ApplicationManager`, `BuildResultCache`, `ClientBuildManager`, `RecycleLimitMonitor`, dynamic assembly helpers |
| Windows service browser-capability installation | 10 | `BrowserCapabilitiesCodeGenerator`; `ServiceController` |
| Remote IIS configuration server | 9 | impersonation and file ACL APIs in `RemoteWebConfigurationHost*` |
| Configuration path helper mismatch | 9 | missing `Debug` and `ConfigPathUtility.GetParent` |
| Windows CNG algorithms | 5 | `MD5Cng`, `SHA*Cng` |
| Data-binding metadata | 3 | `BindableTypeAttribute` |
| Removed database-provider APIs | 3 | SQL notification enlistment, provider permissions, OleDb |
| AppDomain data overload | 2 | three-argument `AppDomain.SetData` |
| ResX build provider | 2 | `ResXResourceReader` |
| Legacy XML load overload | 2 | internal three-argument `XmlDocument.Load` path |
| LOS serialization overload | 1 | byte-array conversion mismatch |
| Role claim provider | 1 | `DynamicRoleClaimProvider` |
| CAS assembly permission metadata | 1 | `Assembly.PermissionSet` |
| **Total** | **79** | |

## Configuration-path resolution

Defining the original `CONFIGPATHUTILITY_SYSTEMWEB` symbol selects the intended
System.Web branch: `GetParent` and the `System.Web.Util.Debug` alias compile.
The mandated build moves 79 errors/2,441 warnings to 70 errors/2,441 warnings.
Imported Reference Source remains unchanged.

The remaining remote IIS host, server, stream, and public server interface are
excluded by decision. A minimal internal host remains only so remote requests
fail explicitly. The mandated build moves 70 errors/2,441 warnings to 61
errors/2,431 warnings.

Windows browser-capability administration and application-level `App_Browsers`
compilation are excluded. A project-owned compiler uses the built-in browser
factory and rejects an `App_Browsers` directory explicitly. The mandated build
moves 61 errors/2,431 warnings to 51 errors/2,419 warnings.

Five CNG-specific constructors in imported `CryptoAlgorithms.cs` use the modern
portable `MD5`/`SHA*` factories instead. Digest output is preserved; concrete
provider identity and OS FIPS policy may differ. The approved five-line patch
moves 51 errors/2,419 warnings to 46 errors/2,419 warnings.

The public `BindableTypeAttribute` contract is copied from pinned Microsoft
Reference Source into project-owned compatibility source. Recompiled consumers
retain runtime auto-field behavior; its assembly identity is `System.Web`, not
Framework `System.ComponentModel.DataAnnotations`. The mandated build moves 46
errors/2,419 warnings to 43 errors/2,419 warnings.

A project-owned internal ResX reader adapts the WinForms Framework-era read
path. It preserves aliases, converters, file references, byte arrays, nulls,
and BinaryFormatter payloads. SOAP and Windows graphics fail explicitly. The
mandated build moves 43 errors/2,419 warnings to 41 errors/2,419 warnings. See
`docs/resx-reader-research.md` for provenance and format decisions.

Two imported XSL call sites use the modern two-argument `Load` overload. Their
Framework-only CAS evidence argument was always null; resolver behavior is
unchanged. The mandated build moves 41 errors/2,419 warnings to 39
errors/2,419 warnings.

The imported LOS parser now checks decoded byte-array length instead of calling
the string-only emptiness helper. This matches the POC correction and intended
non-empty payload guard. The mandated build moves 39 errors/2,419 warnings to
38 errors/2,419 warnings.

The Framework `DynamicRoleClaimProvider` public contract is restored in
project-owned source. Modern `ClaimsIdentity.AddClaims` eagerly snapshots role
claims instead of Framework's unavailable lazy external-claims hook. The
mandated build moves 38 errors/2,419 warnings to 37 errors/2,419 warnings.

`AccessDataSource` retains its public/configuration surface but rejects database
execution explicitly because Jet/ACE OLE DB is Windows COM-only. Diagnostics
direct applications to `SqlDataSource` with a portable provider. The mandated
build moves 37 errors/2,419 warnings to 36 errors/2,419 warnings.

The largest group is architectural: modern .NET cannot reproduce secondary
AppDomain isolation with compatibility members alone. Remote IIS configuration
has already been declared unsupported, so its remaining server files should be
handled consistently rather than shimmed individually. Package/API groups must
still be checked for cross-platform behavior before adding dependencies.

## Warning shape

The 2,441 warnings are also newly complete enough to classify. Largest codes:

| Code | Count | Meaning |
| --- | ---: | --- |
| `SYSLIB0003` | 1,117 | CAS unsupported/obsolete |
| `CS0618` | 504 | obsolete Framework APIs |
| `CA1416` | 450 | Windows-only API reachability |
| `CS0436` | 206 | local `System.Web` types conflict with modern facade types |

Warning cleanup should follow compile-error architecture decisions; suppressing
these categories globally would hide real compatibility boundaries.

## Artifacts and validation

Machine-readable output is under
`artifacts/build/net10.0/exposed-wave-inventory/`: complete diagnostics,
message groups, compiler-code counts, subsystem counts, forwarded assemblies,
and JSON summary. Imported Reference Source remains unchanged.
