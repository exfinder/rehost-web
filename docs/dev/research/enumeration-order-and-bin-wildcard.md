# Filesystem enumeration order and wildcard `bin` loading

Evidence for ledger P73, landed 2026-08-16. Current behavior lives in the
[portability ledger](../portability-ledger.md); this document preserves the
audit and Framework readings.

## Result

Directory enumeration is deterministic on every filesystem:
`OrdinalIgnoreCase` by name with an `Ordinal` tiebreak. Batch provider maps
preserve insertion order. Wildcard `bin` loading matches `.DLL`
case-insensitively, skips files without managed metadata, and surfaces genuine
architecture/identity failures.

The contract is determinism, not exact Framework sequence. Framework inherited
NTFS index order, then sometimes scrambled it through hash/object-identity
collections; exact parity was not stable to reproduce.

## Enumeration audit

Most System.Web scans share port-owned `Util/FileEnumerator`; direct BCL calls
are the exceptions.

| Reached consumer | Source | Observable effect | Treatment |
| --- | --- | --- | --- |
| `App_Code`, global/local resources, Web References | `CodeDirectoryCompiler` via `VirtualDirectory.Children` | Which duplicate declaration/resource is blamed; source order | Sorted `FileEnumerator` |
| Page-directory batch/non-batch compilation | `WebDirectoryBatchCompiler`, `NonBatchDirectoryCompiler` | First cached compile failure | Sorted enumeration plus insertion-ordered provider map |
| Themes | `ThemeDirectoryCompiler` | CSS `<link>` order; duplicate skin blamed | Sorted `FileEnumerator` |
| Precompilation traversal/themes | `BuildManager` | First failing directory/theme | Sorted shared/direct scans |
| Directory hashes | `HashCodeCombiner` | Cross-run codegen cache reuse | Sorted `FileEnumerator` |
| `<add assembly="*"/>` | `CompilationSection` direct `GetFiles("*.dll")` | Reference/activation failure order | `BinDirectoryScan` |
| OWIN startup scan | Katana `DefaultLoader` | Competing startup discovery | Left under Katana provenance |

Order-irrelevant scans delete/copy complete sets, test directory emptiness, or
build sets. Browser-definition and Optimization scans already sort. App
Browsers, dynamic discovery, Web References generation, FCN, and native preload
branches are unsupported or unreachable. `CanonicalCasePath` deliberately
collects every case-insensitive match before reporting a collision.

### Why order is observable

- Theme CSS enumeration becomes rendered `<link>` order, affecting cascade.
- Duplicate skins/resources and App_Code types blame the later input.
- `HashCodeCombiner` folds entries sequentially, so raw filesystem order can
  invalidate generated-code caches across runs.
- Batch compilation formerly keyed providers in a randomized `Hashtable`;
  sorting the directory alone did not stabilize the later `.Values` walk.

### Framework/NTFS baseline

NTFS directory indexes compare file-name code units after applying the volume's
`$UpCase` table; Framework enumeration exposed that order. For ASCII names,
`OrdinalIgnoreCase` reproduces it, while the ordinal tiebreak handles names a
case-sensitive filesystem can store but NTFS cannot. Non-ASCII order can differ
between NTFS volumes because `$UpCase` is fixed when the volume is formatted.
The Win32/.NET APIs never guaranteed enumeration order, and FAT, ReFS, ext4,
and APFS differ.

Linguistic sorts used by browser-capability code are deterministic for their
purpose but are not this filesystem contract.

## Wildcard `bin` loading

The shipped `<add assembly="*"/>` is forced during pre-application start through
`CompilationSection.LoadAllAssembliesFromAppDomainBinDirectory`; failures are
cached and replayed on later requests. The scan loads by simple name through
the port's default-context resolver, not directly by path. `<remove>` filters
names before loading; `bin` subdirectories are excluded.

Framework ignored only `COR_E_ASSEMBLYEXPECTED`, intended for native DLLs.
Modern .NET reports non-managed images as `COR_E_BADIMAGEFORMAT`; widening the
catch would also hide architecture failures. P73 instead probes PE metadata
before loading and preserves all genuine loader failures.

### Framework readings

Captured 2026-08-16 on IIS 10 / Framework 4.8.9344. Each application carried
only `<add assembly="*"/>` plus the stated files.

| `bin` input | Framework outcome |
| --- | --- |
| Native PE beside `A.dll` | 200; `A` referenced; native file ignored |
| Zero-byte/text `.dll` beside `A.dll` | 200; invalid files ignored |
| Culture=`de` `Foo.resources.dll` | 200; resource assembly referenced |
| 32-bit-required `X86Lib.dll` | Cached 500 Configuration Error naming incorrect format and wildcard config line |
| `A.dll` plus identical `A.copy.dll` | Cached 500 manifest-definition mismatch for `A.copy` |
| `A.dll`, `B.dll`, remove `B` before wildcard | 200; `A` once, `B` absent |

Port tests cover non-managed files, upper-case extensions, architecture and
identity failures, remove semantics, deterministic enumeration, and App_Code
duplicate blame. Runtime-owned assemblies retain precedence over `bin`, matching
the project's assembly-load contract rather than application override.

## Recorded boundaries

- OWIN startup discovery retains Katana's enumeration behavior.
- Object-identity dependency buckets and `HybridDictionary` behavior remain as
  Framework had them; only the string-keyed randomized map changed.
- An unreadable Unix `bin` fails activation.
- Public behavior does not promise an order for
  `BuildManager.GetReferencedAssemblies()`, which is a set.
