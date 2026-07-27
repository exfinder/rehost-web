# Portable filesystem and configuration-path semantics

Status: open. Priority: high. Depends on portable path mapping.

## Problem

Framework helpers distinguish missing, file, directory, inaccessible, invalid,
and indeterminate paths. `File.Exists`/`Directory.Exists` often collapse those
states. Configuration caches historically normalize casing, which breaks Unix
paths and may alias distinct files.

## Current state

`FileEnumerator`/`FindFileData`/`FileAttributesData` are portable (ledger P23),
built on `DirectoryInfo.EnumerateFileSystemInfos` and `FileSystemInfo`.

`FileAttributes.Hidden` classifies Unix dot-files differently from Windows.
That can exclude deployed content such as `.well-known` on Unix while including
repository metadata on Windows. The divergence is unreachable in the
precompiled-handler slice; dynamic compilation must choose an explicit,
cross-platform content-selection policy.

## Required decisions

- Required filesystem result model for mapping, compilation, and config lookup.
- Error behavior for missing, inaccessible, malformed, and race-lost paths.
- Enumeration ordering, links, and case comparison.
- Whether hidden-file exclusion stays an OS attribute query or becomes an
  explicit name list, per the divergence recorded above.
- Configuration cache keys without corrupting physical casing.
- Wildcard assembly loading behavior; never swallow unrelated load failures.

## Verification

Focused tests cover each path state, file/directory confusion, case-distinct
files, disappearing files, inaccessible paths where testable, and wildcard
assembly failures.

## Done when

First-request filesystem decisions preserve observable distinctions needed by
System.Web and produce actionable diagnostics.
