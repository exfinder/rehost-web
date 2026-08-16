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

## Required decisions

- Separate URL, virtual, configuration, and physical path value rules.
- Cross-filesystem enumeration ordering and wildcard `bin` assembly-loading
  failures, without swallowing unrelated load errors.

## Done when

Enumeration order and `bin` wildcard loading are deterministic across
filesystems, and the value rules are written down where a new seam can find
them.
