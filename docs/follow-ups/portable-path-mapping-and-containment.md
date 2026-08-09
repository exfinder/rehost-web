# Portable path mapping and containment

## Problem

System.Web mixes URL paths, configuration paths, and physical paths. Replacing
backslashes mechanically is insufficient. Prefix comparisons, `..`, encoded
separators, alternate separators, virtual roots, symlinks, and case-sensitive
filesystems can change behavior or escape the application root.

## Required decisions

- Separate URL, virtual, configuration, and physical path value rules.
- Canonicalization and root-containment algorithm.
- Platform-aware case semantics without lowercasing real paths.
- Path-info and extension boundary parsing.
- Symlink policy and behavior for invalid/encoded traversal.
- Compatibility behavior for Windows-shaped input on non-Windows hosts.
- Explicit known-physical seams when reached in server includes, configured
  master pages, sitemap providers, controls/data sources/mail, and disabled FCN
  call sites; rooted Unix strings cannot identify intent by shape.
- Cross-filesystem enumeration ordering and wildcard `bin` assembly-loading
  failures, without swallowing unrelated load errors.

## Verification

Table-driven tests cover root/sub-app mapping, mixed case, Unicode, dot
segments, encoded separators, sibling-prefix attacks, trailing separators, and
symlink escape where supported.

## Done when

All first-request translations remain inside configured roots and preserve
valid platform path casing.
