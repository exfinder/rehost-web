---
status: accepted
---

# Resolve managed assemblies from application bin

The portable runtime owns one immutable fallback resolver on the default
`AssemblyLoadContext`. It resolves managed assemblies only from the explicit
application `bin` directory after normal runtime resolution fails.

Before publication, activation inventories managed assembly identities and
rejects ambiguity or conflicts with runtime contract assemblies. The resolver
serves the process-scoped immutable application generation; shadow copying,
replacement, unloading, native dependencies, and binding redirects are outside
the first slice.

The first-slice fixture's handler assembly exists only in application `bin`.
The Kestrel host must not reference it. This avoids the incidental load path
used by the sibling Portable.System.Web POC, where the web application is
compiled into the host executable.
