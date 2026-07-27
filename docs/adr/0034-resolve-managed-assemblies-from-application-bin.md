---
status: accepted
---

# Resolve managed assemblies from application bin

The portable runtime owns one immutable fallback resolver on the default
`AssemblyLoadContext`. It resolves managed assemblies only from the explicit
application `bin` directory after normal runtime resolution fails.

Activation does not inventory or reject assembly identities. .NET Framework had
no such gate: Fusion preferred already-loaded assemblies, then the GAC, and only
then probed `bin`, so a shared assembly present in both locations resolved to
the shared copy without error. Fallback ordering reproduces that precedence
directly — runtime-owned assemblies resolve first and win — and a genuine
version conflict fails at load with the runtime's own mismatch error, which is
the Framework diagnostic. Declining to load a `bin` file is reported on the
runtime diagnostic channel, not enforced.

The resolver serves the process-scoped immutable application generation; shadow
copying, replacement, unloading, native dependencies, and binding redirects are
outside the first slice.

The first-slice fixture's handler assembly exists only in application `bin`.
The Kestrel host must not reference it. This avoids the incidental load path
used by the sibling Portable.System.Web POC, where the web application is
compiled into the host executable.
