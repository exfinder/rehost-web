# ResX reader compatibility

## Contract

System.Web uses the .NET Framework-era WinForms ResX implementation from
dotnet/winforms as its behavioral baseline.
The imported `SYSTEM_WEB` profile handles strings, whitespace, primitives,
nulls, aliases, metadata, data nodes, relative file references, and common type
conversion. Legacy serialized and drawing paths remain but their breadth and
platform behavior are unassessed.

Imported source identity and license are in [sources](sources.md).
`ResXBuildProvider` uses the reader under `src/Rehost.Web/Compatibility/Resources`.

## Deliberate deviation

The Framework-directory cache test uses
`RuntimeEnvironment.GetRuntimeDirectory()` instead of the Windows-only
`%SystemRoot%\Microsoft.NET\Framework` path. This changes only which resolved
runtime types may be cached.

Legacy BinaryFormatter/Soap and drawing payloads remain trusted-input
compatibility behavior. They are not safe for untrusted `.resx` files and can
remain platform-limited.

Uncovered edge cases:
[ResX compatibility](follow-ups/resx-compatibility.md).
