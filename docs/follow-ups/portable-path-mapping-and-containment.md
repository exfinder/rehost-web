# Portable path mapping and containment

## Problem

System.Web mixes URL paths, configuration paths, and physical paths. Replacing
backslashes mechanically is insufficient. Prefix comparisons, `..`, encoded
separators, alternate separators, virtual roots, symlinks, and case-sensitive
filesystems can change behavior or escape the application root.

## Decided

Ledger P57/P70/P71/P72 and the
[IIS URL canonicalization readings](../research/iis-url-canonicalization-readings.md):
http.sys-equivalent canonicalization at the adapter (`\\`, `%2F`, repeated
separators, dot segments, above-root 403), IIS's handler-mapping path-info
split, sibling-prefix and root containment (`AspNetCoreWorkerRequestTests`),
symlinks/junctions followed as IIS follows them with `..` through them still
contained, case folding below the nearest existing directory, and
Windows-name rules as portable application rules. Remaining differences are
IIS-native-tier and recorded on the
[IIS-role follow-up](iis-role-behaviors.md).

Ledger P73 and the
[enumeration research](../research/enumeration-order-and-bin-wildcard.md):
every directory listing sorts in NTFS order, batch page compilation keeps the
directory's order off Framework, and the wildcard `bin` scan matches `.DLL` on
every filesystem and skips non-assembly files before loading — every other
load failure surfaces as Framework surfaced it.

The value rules for URL, virtual, configuration, and physical paths are the
"Path value rules" section of [filesystem semantics](../filesystem-semantics.md);
the verification matrix (root/sub-app mapping, mixed case, dot segments, encoded
and repeated separators, sibling prefixes, trailing separators, symlink
containment) is `RequestPathCanonicalizerTests`, `RequestPathInfoTests`,
`AspNetCoreWorkerRequestTests`, and `CanonicalCasePathTests`; NFD on APFS is a
documented trait. This follow-up is closed; what is below stays only as
recorded boundaries.

## Recorded boundaries

- `CodeDirectoryCompiler`'s `BuildProviderSet` (`HybridDictionary`) keeps
  insertion order only to eight entries, and `ProcessDependencies` buckets by
  object identity — App_Code compile order past eight files is deterministic
  per name set only as far as the runtime's `HybridDictionary` is; Framework
  had the same behavior. Reopen only if a consumer observes it.
- The OWIN `Startup` scanner (`Owin.Host.SystemWeb/Loader/DefaultLoader.cs`)
  enumerates `bin` unsorted; Katana provenance rules apply.
- Truncated managed images: the pre-load metadata probe skips a file
  `PEReader` cannot open, where Framework reported `COR_E_BADIMAGEFORMAT`.
