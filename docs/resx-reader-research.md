# Cross-platform ResX reader research

## Implemented decision

Compatibility follows the .NET Framework-era WinForms implementation from
`dotnet/winforms` commit
`195f89af79d550c2da1711c45c379efd63519ac1`. This initial source import retains
the `SYSTEM_WEB` profile used by the shared Framework implementation and is the
behavioral baseline; later WinForms and MSBuild implementations are research
references, not the semantic target.

The donor files were imported unchanged in `516ed69`, minimally adapted to
build on .NET 10 in `62fb8e9`, moved under
`Compatibility/Resources`, and connected to `ResXBuildProvider` in `8999fa6`.
The complete donor reader closure was retained, including data nodes, file
references, type conversion, writer/resource-set support, drawing integration,
and legacy BinaryFormatter/Soap paths. `System.Drawing.Common` and
`System.Runtime.Serialization.Formatters` provide required APIs; drawing
operations can still be platform-limited. Serialized payloads are a trusted
input compatibility feature, not a security boundary.

One deliberate platform adaptation replaces the donor's
`%SystemRoot%\Microsoft.NET\Framework` cache test with
`RuntimeEnvironment.GetRuntimeDirectory()`. It changes only which resolved
runtime types may be cached and works on every supported OS (`d09d5f7`).

Twenty focused tests pin reader behavior. The full runtime suite passed 37/37
on .NET 10 arm64 when this implementation was completed on 2026-07-22.

## Earlier recommendation (superseded)

The initial proposal favored a narrow reader informed by current WinForms and
MSBuild, rejecting legacy serialization and Windows-only values. It was
superseded by the decision to preserve the `195f89a` behavior wherever it can
build on .NET 10.

## Evidence

### Framework contract

- Reference Source `System.Web/Compilation/ResXBuildProvider.cs` creates `ResXResourceReader(Stream)`, sets `BasePath` from `HostingEnvironment.MapPath(VirtualPath)`, then returns it as `IResourceReader`. Therefore Web Forms needs values during dynamic resource compilation, plus relative file-reference resolution; reader/writer public API parity is unnecessary. Source: local Microsoft Reference Source clone, `/Users/vm/repos/referencesource/System.Web/Compilation/ResXBuildProvider.cs`.
- The portable POC reached the same seam by adding a separate `Portable.System.Resources` project copied from Mono. Its README records this explicitly. Its reader handles strings, null, byte arrays, file references, type converters, aliases, metadata, and serialized payloads. Source: `/Users/vm/repos/Portable.System.Web/Portable.System.Web/README.md` and `/Users/vm/repos/Portable.System.Web/Portable.System.Resources/`.
- POC unsuitable unchanged: substantial reader/writer/node surface; `System.Drawing` types; direct `BinaryFormatter.Deserialize`; Soap payload only throws. It also suppresses `SYSLIB0011`. Sources: POC README; `SerializedFromResXHandler.cs`; `ResXDataNode.cs`.

### Current Microsoft implementation

- [`MSBuildResXReader`](https://github.com/dotnet/msbuild/blob/main/src/Tasks/ResourceHandling/MSBuildResXReader.cs) is current, cross-platform Microsoft code. XML parsing disables resolution (`XmlResolver = null`) and ignores DTDs. It recognizes assembly aliases, strings, null, `byte[]`, file references, type-converter strings/bytes, legacy binary MIME types, and rejects unknown MIME types on Core.
- Its file-reference handling covers strings with optional encoding, byte arrays, memory streams, and deferred typed objects. This closely matches the needed format subset.
- Not reusable as an API: class is `internal`; output is MSBuild-internal `IResource` variants; signatures depend on `TaskLoggingHelper`, MSBuild file utilities, and task resource messages. Copy/adapt only the narrow parsing logic under the [MSBuild MIT license](https://github.com/dotnet/msbuild/blob/main/LICENSE), recording source commit and notices.
- Microsoft documents that `.resx` may contain insecure BinaryFormatter data and recommends only trusted inputs. [`GenerateResource`](https://learn.microsoft.com/en-us/visualstudio/msbuild/generateresource-task) also documents linked files, typed values, type resolution, and `System.Resources.Extensions` for preserialized resources.

### Current WinForms source clone

- Inspected `/Users/vm/repos/winforms` at commit `d28310fdba71b603f53edf92907e7248a652fa06`. All relevant files carry .NET Foundation MIT headers; repository `LICENSE.TXT` is MIT.
- Current reader remains a close behavioral match for Framework Web Forms: stream constructor, `BasePath`, eager enumeration, header validation, assembly aliases, metadata, duplicate-name last-wins behavior, whitespace preservation, type resolution, invariant `TypeConverter`, `byte[]`, null, and file references. Primary files: `ResXResourceReader.cs`, `ResXDataNode.cs`, `ResXFileRef.cs`, `ResxFileRef.Converter.cs`.
- Direct reader closure is about 1,963 lines across ten resource files before shared helpers/messages: reader + alias resolver, data node, file-ref + converter, null marker, serialization binder, alias interface, node info, and assembly-name type resolver. It additionally uses `MultitargetUtil`, exception helpers, SR messages, and `System.Drawing.Point`.
- Full copy is not cross-platform/minimal. Binary payload support pulls `System.Formats.Nrbf`, private Windows NRBF serializers, WinForms-specific records, and a `BinaryFormatter.Deserialize` fallback. File references special-case `Bitmap`/`Icon`; arbitrary other types use reflection to invoke a public stream constructor. Writer-related methods add further serialization dependencies that Web Forms reading does not need.
- Practical port seam: retain the XML reader and read-side portions of data-node/file-ref resolution; replace writer constants with local format constants; replace/remove write-side constructors and serialization; use a tiny line-position value instead of WinForms-oriented helpers; remove bitmap/icon branch; reject binary/Soap/unknown MIME types before object materialization. Keep general `TypeConverter` and non-Windows stream-constructor file refs for maximum source compatibility, subject to explicit architecture approval because these execute application type code.
- Current WinForms behavior is not itself the desired security contract: unsupported non-binary MIME can resolve to `null`, while binary data may fall back to unsafe deserialization. Rehost guardrails require descriptive failure instead.

### .NET Framework Reference Source availability

- Inspected `/Users/vm/repos/referencesource` at commit `ec9fa9ae770d522a5b5f0607898044b7478574a3`. This clone contains the `System.Web` call site but no WinForms `ResXResourceReader`, `ResXDataNode`, or `ResXFileRef` implementation. The call site confirms required behavior, but exact Framework reader provenance cannot come from this local clone.
- `System.Web/System.Web.txt` says its ResX error strings correspond to code pulled from `fx/src/WinForms/Managed/System/Resources`; this corroborates the historical boundary, not source availability.
- The WinForms repository's initial source import, commit `195f89af79d550c2da1711c45c379efd63519ac1` (2018-11-15), contains the legacy reader/data-node/file-ref implementation. That reader has an explicit `SYSTEM_WEB` branch making the shared implementation internal under `System.PrivateResources`, strongly matching the missing Framework donor lineage. The full Git history therefore supplies both the legacy compatibility baseline and the current modernized implementation under MIT.

### NuGet options

- [`ResXResourceReader.NetStandard` 1.3.0](https://www.nuget.org/packages/ResXResourceReader.NetStandard/) is MIT, targets `netstandard2.0/2.1`, and has ~4.3M total downloads. Latest release: 2024-03-05. It is an almost-direct WinForms source copy under `System.Resources.NetStandard`; 1.2 added file-resource fixes, 1.3 null support. Bitmap behavior remains platform-dependent. Viable reference/POC, but adds ~406 KB package, public reader+writer surface, old copied implementation, and broader object materialization than desired.
- `Curiosity.Resources` is another WinForms extraction, last ResX-specific release found: 2021, far lower adoption. No advantage over the above or current Microsoft source.
- `System.Resources.Extensions` supports preserialized binary `.resources`; it does not supply a public cross-platform ResX XML reader. It solves a later runtime-resource format concern, not this `ResXBuildProvider` input seam.

## Verified compatibility baseline

Focused tests cover strings and significant whitespace, primitive conversion,
legacy `mscorlib` byte arrays, nulls, duplicate-name behavior, assembly aliases,
metadata, data-node mode, relative file references, setting locks, disposal,
malformed input, header validation, and the `SYSTEM_WEB` reader/writer-header
rule. Binary/Soap payloads and drawing values retain donor behavior but are not
yet covered by this focused suite.

Future changes should treat `195f89a` behavior as the default. Deviations should
be limited to build or platform requirements and recorded explicitly.
