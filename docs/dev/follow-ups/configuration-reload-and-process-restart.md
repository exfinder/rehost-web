# Configuration reload and process restart

## Problem

Bootstrap configuration is immutable and file watching is disabled. Framework
normally responds to relevant configuration changes by replacing the
application AppDomain; current-AppDomain Rehost cannot safely reset static
state.

## Required decisions

- Which machine, root-web, application, and `configSource` files are watched.
- Debounce, atomic replacement, editor-write, rename, and inaccessible-file
  behavior across supported filesystems.
- Host restart notification and shutdown reason contract.
- Behavior when the host cannot replace the process.
- Diagnostics and loop protection for repeatedly invalid replacement config.
- `FileChangesMonitor`'s six `UrlPath.IsAbsolutePhysicalPath(alias)` checks
  (past today's `IsFCNDisabled` return) refuse every Unix-rooted alias with
  `E_INVALIDARG`; enabling notification needs a rooted-path test there
.

## Done when

A configuration change requests process replacement through the host lifecycle
contract; Rehost never attempts in-process config reload or static-state reset.
