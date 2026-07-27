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

One divergence is carried deliberately. `FileData.IsHidden` tests
`FileAttributes.Hidden`, which .NET sets for dot-files on Unix but derives from
the NTFS attribute on Windows. The same source tree therefore classifies a
different set of entries per OS:

- Unix-only exclusions that help: `.git`, `.svn`, `.vs`, `.DS_Store` drop out of
  batch compilation and out of the precompile file copy. On Windows they are
  carried along, which is why Framework accumulated hardcoded name exclusions
  such as `_vti_cnf` (`BuildManager.PrecompileWebDirectoriesRecursive`).
- Unix-only exclusions that hurt: `.well-known/` is a deliberate part of a
  deployed application and would silently vanish from a precompile performed on
  Unix while surviving one performed on Windows.

The consumer is `MapPathBasedVirtualPathProvider`, whose enumeration decides
application *content*: batch compilation, `App_Code`, `App_Themes`,
`App_Browsers`, `App_WebReferences`, and the precompile copy. None of those run
while the supported fixture uses a precompiled handler, so the divergence is
currently unreachable.

The attribute query is kept because filesystem convention is not a leaf seam's to
invent, and normalizing to Windows semantics would pull `.git` into the
application. The real decision belongs to the compilation slice: whether content
selection should use an explicit name-based exclusion list — the mechanism
Framework already reaches for — instead of an OS attribute.

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
