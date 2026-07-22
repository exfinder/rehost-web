# WebResource assembly timestamps

## Status

Follow-up required. `SYSLIB0044` is temporarily suppressed for the Runtime
project to preserve imported `AssemblyResourceLoader` behavior.

## Compatibility risk

`AssemblyResourceLoader` reads `AssemblyName.CodeBase`, converts its URI to a
local path, then uses the file modification time for:

- the `t` cache-busting value in generated `WebResource.axd` URLs;
- the response `Last-Modified` value.

Exact warning site: `Handlers/AssemblyResourceLoader.cs:152`. This path is live
for embedded scripts, images, and other WebResources.

`CodeBase` is obsolete on modern .NET. The imported implementation also assumes
a file-backed assembly. Bundled, in-memory, or otherwise locationless assemblies
may not provide a usable path, causing resource URL generation or serving to
fail.

## Required design

Compare these policies:

- use `Assembly.Location` and preserve file modification timestamps;
- reject locationless assemblies with an actionable unsupported exception;
- assign a process-lifetime timestamp to locationless assemblies;
- derive a deterministic resource version from assembly identity or content.

The chosen value must keep URL generation stable, invalidate caches after
deployment, produce a valid HTTP date, and support the intended publishing
models.

## Completion criteria

- Define supported file-backed, bundled, and in-memory assembly models.
- Verify WebResource URL stability and cache invalidation across restarts and
  deployments.
- Replace `CodeBase` or explicitly reject unsupported publishing models.
- Remove the temporary `SYSLIB0044` suppression.
