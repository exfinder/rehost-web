# Configuration reload and process restart

## Problem

Bootstrap configuration is immutable and file watching is disabled. Framework
normally responds to relevant configuration changes by replacing the
application AppDomain; current-AppDomain Rehost cannot safely reset static
state.

## Evidence

DNN Platform 10.3.3 ([issue 29](https://github.com/exfinder/rehost-web/issues/29),
item 4) writes its own root `web.config` through one `Config.Save` and relies
on the file-change restart as the commit: the install and upgrade wizards write
the machine key, connection string, `InstallVersion` and version-specific
`XmlMerge` fragments, then `Response.Redirect` to themselves expecting the next
request in a new process, because every read-back goes through the
process-cached `ConfigurationManager`. Extension installs merge manifest
`<config>` nodes, binding redirects and `codeSubDirectories`, then touch the
file only when `fcnMode` reads `Disabled`. The "Restart application" actions
are a touch of `web.config`; no DNN code calls `HttpRuntime.UnloadAppDomain`.
Without the watch the wizard's next request runs on the old configuration and
the operator needs an external watcher, which races the wizard's redirect.

## Required decisions

- Which machine, root-web, application, and `configSource` files are watched.
- Debounce, atomic replacement, editor-write, rename, and inaccessible-file
  behavior across supported filesystems.
- Host restart notification and shutdown reason contract.
- Behavior when the host cannot replace the process.
- Diagnostics and loop protection for repeatedly invalid replacement config.
- Whether to keep refusing `fcnMode` values other than `Disabled` once the
  watch exists. Today preflight refuses them because nothing honors them. DNN
  writes `fcnMode="Single"` into `web.config` during install, so the refusal
  can only be answered by patching the application until the watch lands and
  the value selects it (decided 2026-10-04: keep the refusal until then).
- `FileChangesMonitor`'s six `UrlPath.IsAbsolutePhysicalPath(alias)` checks
  (past today's `IsFCNDisabled` return) refuse every Unix-rooted alias with
  `E_INVALIDARG`; enabling notification needs a rooted-path test there
.

## Done when

A configuration change requests process replacement through the host lifecycle
contract; Rehost never attempts in-process config reload or static-state reset.
