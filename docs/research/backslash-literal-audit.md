# Backslash-literal audit

Evidence for ledger P74/P75. The 2026-08-16 audit asked which compiled `\`
literals were intended as physical separators and would fail on Linux/macOS.

## Result

| Class | Sites | Outcome |
| --- | ---: | --- |
| Reached physical defects | 8 across 2 defects | Fixed and tested |
| Unreached physical defects | 32 across 16 rows | Safe trivial cases fixed; others recorded |
| Correct contract uses | 195 | Kept |
| Dead/uncompiled uses | 51 | Kept |

Total: 286 unique `file:line` sites. P74 owns separator fixes; P75 replaces
native process-name discovery.

## Scope and method

Audited imported System.Web, Web Services, Optimization, Extensions, and
Application Services trees plus all Rehost runtime, hosting, OWIN, Friendly
URLs, and companion-assembly sources. Project include/remove rules and active
preprocessor symbols determined what compiled.

A comment-aware C# literal lexer found 663 candidates; 94 generated `obj`
occurrences and 265 numeric escape artefacts were excluded. Every physical-path
candidate was traced to callers. An inverse sweep checked
`Path.DirectorySeparatorChar`, `Path.Combine`, and related APIs for accidental
use on virtual paths.

Only four Web Services configuration files and `WsiProfiles.cs` compiled at the
time; Extensions used an explicit closure excluding Client Services and WCF
compilation. `NETFRAMEWORK`, `DBG`, `PLATFORM_UNIX`, `FEATURE_PAL`, and
`USE_MEMORY_CACHE` were not defined.

## Reached defects

### Long physical paths

`FileUtil.IsSuspiciousPhysicalPath` treated any `/` as suspicious and walked
segments using `\`. On Unix every physical path contains `/`, so canonical
paths over 259 characters returned 404. The portable branch now accepts the
native separator while retaining embedded-colon and parent-segment refusals.
Tests cover a canonical long path and a climbing path.

Sites: `Util/FileUtil.cs:214,218,225,241`.

### `SimpleWorkerRequest`

The public worker request composed translated paths with `\`, yielding names
such as `/app/sub\page.aspx` off Windows. It now uses
`Path.DirectorySeparatorChar`; tests construct the public request directly and
assert `Path.Combine`-equivalent translation.

Sites: `Hosting/SimpleWorkerRequest.cs:162,215,411,412`.

## Unreached findings

| Row | Area | Why not reached | Disposition |
| --- | --- | --- | --- |
| B1 | CBM generated-source directory | Designer/`ClientBuildManager` only | Fixed separator |
| B2 | Trust policy directory | Full trust required | Fixed separator |
| B3 | FCN subdirectory watch | FCN disabled/refused | Fixed; reload follow-up owns reachability |
| B4-B5 | Express/IIS map-path drive roots | IIS host only | Windows-shape boundary |
| B6 | `ProcessHostMapPath` result | IIS host only | Fixed separator |
| B7 | IIS 6 metabase mapping | Excluded profile | Recorded |
| B8-B11 | AppDomain/ProcessHost/ISAPI/IIS7 worker paths | COM/native hosts absent | Recorded |
| B12 | `WebProcessInformation` | Native process lookup failed first | Replaced by `Environment.ProcessPath`/`ProcessId` (P75) |
| B13 | LocalDb attachment path | SQL user instances are Windows-only | Recorded boundary |
| B14 | AzMan XML role store | COM assembly absent | Recorded boundary |
| B15 | ResX writer base path | Runtime reads but does not write ResX | Fixed separator |
| B16 | Dynamic physical discovery | Source not compiled; no `.vsdisco` handler | Fixed safe compositions; feature still unsupported |

## Correct uses retained

| Category | Examples and contract |
| --- | --- |
| Virtual/URL normalization | `UrlPath`, `VirtualPath`, configuration paths, request canonicalization, Friendly URLs, bundles; both slash forms are intentional input syntax and output normalizes to `/` |
| UNC/drive-shape checks | `X:\` and `\\server\share` remain Windows physical shapes; off Windows those strings stay virtual per P71 |
| Wire/string escaping | JavaScript, CSS, LDAP, cache keys, WebSocket tokens, ASP-compat encoding, VB command lines |
| Regular expressions | Browser capabilities, validators, login controls, wildcard matching, Optimization patterns |
| Account/domain names | `DOMAIN\user` in membership/role providers |
| Registry paths | Registry vocabulary, independently guarded or unavailable off Windows |
| Non-filesystem value paths | Tree/menu control-state paths |
| Filename and synchronization rules | Portable Windows-strict filenames; `Global\`/`Local\` mutex namespaces |
| Deliberately Windows-shaped diagnostics | Detection of Windows-rooted `SaveAs` input off Windows |
| Metadata/prose | Dummy `#line`, suppressions, generated-identifier replacement, date/time format escaping |

The key retained boundary is `UrlPath.PathIsDriveRoot`: `/` is not a Windows
drive root, and hosting an application at filesystem root remains unsupported.

## Dead/uncompiled groups

- Framework-only `FileEnumerator`, template-parser, LOS formatter, and
  multi-targeting branches.
- Debug-only helpers, Framework-only machine-wide browser generation, and
  project `Compile Remove` files for remote configuration,
  strong-name/registration utilities, and transactions.
- IIS/Framework-install discovery and almost all legacy Web Services discovery
  source.
- Extensions Client Services/WCF build-provider files outside the compiled
  closure.

## Inverse check

No active Rehost code used physical-path APIs on a virtual path. The one mixed
imported case, Optimization's `FileExtensionReplacementList`, calls
`Path.Combine` on a virtual value then normalizes `\` to `/`; Windows needs the
repair and Unix already produces `/`, so both are correct.

## Recorded boundaries

- LocalDb/user-instance filename behavior remains Windows-only.
- Application root `/` remains unsupported.
- Dormant IIS/COM/FCN paths must be re-audited before their feature gate opens.
