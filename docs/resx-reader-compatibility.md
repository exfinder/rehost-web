# ResX reader compatibility

## Contract

System.Web uses the .NET Framework-era WinForms ResX implementation from
`dotnet/winforms` commit
`195f89af79d550c2da1711c45c379efd63519ac1` as its behavioral baseline.
The imported `SYSTEM_WEB` profile supports strings, whitespace, primitives,
nulls, aliases, metadata, data nodes, relative file references, type
conversion, and legacy serialized/drawing values.

The donor closure lives under
`src/Rehost.WebForms.Runtime/Compatibility/Resources/WinForms195f89a/`;
its attribution is in `SOURCE.md`. `ResXBuildProvider` uses the project-owned
reader through `src/Rehost.WebForms.Runtime/Compatibility/Resources`.

## Deliberate deviation

The Framework-directory cache test uses
`RuntimeEnvironment.GetRuntimeDirectory()` instead of the Windows-only
`%SystemRoot%\Microsoft.NET\Framework` path. This changes only which resolved
runtime types may be cached.

Legacy BinaryFormatter/Soap and drawing payloads remain trusted-input
compatibility behavior. They are not safe for untrusted `.resx` files and can
remain platform-limited.

Tests:
`tests/Rehost.WebForms.Runtime.Tests/ResXResourceReaderTests.cs`.

Uncovered edge cases:
[ResX compatibility](follow-ups/resx-compatibility.md).
