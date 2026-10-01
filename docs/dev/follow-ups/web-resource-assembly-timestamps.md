# WebResource assembly timestamps

## Problem

`AssemblyResourceLoader` uses obsolete `AssemblyName.CodeBase` and assumes a
file-backed assembly when generating WebResource cache keys and
`Last-Modified`. Bundled/in-memory assemblies may have no usable path.

## Required contract

Choose behavior for file-backed, bundled, and in-memory assemblies: file
timestamp, deterministic content/identity version, process timestamp, or
explicit rejection.

The value must remain stable, invalidate after deployment, produce valid HTTP
dates, and fit supported publishing models.

## Done when

- Publishing models and timestamp/version behavior are documented and tested.
- `CodeBase` is removed or unsupported models fail clearly.
- Temporary `SYSLIB0044` suppression is removed.
