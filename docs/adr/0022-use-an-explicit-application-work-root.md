---
status: accepted
---

# Use an explicit application work root

Each application runtime receives an explicit writable work root. The existing
`HttpRuntime.SetUpCodegenDirectory` sequence remains, but its nonportable
AppDomain/temp-directory leaf resolves a deterministic generation-specific
directory beneath that root.

Configured `compilation/tempDirectory` values must agree with the host-owned
root or fail as conflicting ownership. Runtime assembly location, source roots,
and ambient temporary directories are not defaults.
