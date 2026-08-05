# Runtime code generation and loading

Status: resolved for slice 2. Dynamic page compilation is resolved by slice 3.

## Problem

A dynamic `.aspx` request reaches CodeDOM generation, compiler selection,
reference resolution, output cleanup, assembly loading, and cache invalidation.
Modern AppDomain probing, dynamic-directory, and shadow-copy setters are
ineffective. Imported paths also assume Windows separators, native file
enumeration, Win32 resources, and IIS-owned temporary directories.

## Decided

The codegen directory, its generation segment, reuse, and reclamation are
[ADR 0042](../adr/0042-derive-the-codegen-directory-from-the-application.md);
ledger row P08 records the leaf. The gate for this work is port-local by
[ADR 0043](../adr/0043-gate-the-compilation-substrate-locally.md).

- Root: host `CompilationTempDirectory`, then configured `tempDirectory`, then
  `{Path.GetTempPath()}/rehost-webforms-tempfiles`. Disagreement between the
  first two fails at preflight; an unwritable root fails naming its source.
- Segment: eight hex characters derived from the application directory, so the
  path is stable across restarts and distinct applications never collide.
- Reuse: Framework's `hash/hash.web` comparison is retained unchanged. An
  unchanged application restarts without recompiling, which required two
  per-process randomized hashes to be replaced (P37, P38).
- Cross-process: `CompilationLock` and `.delete` markers behave as Framework
  defined them. Cross-process exclusion depended on P38, without which every
  process derived a different mutex name.
- Loading: generated assemblies enter through `GeneratedAssemblyLoader` into the
  one load context (P34), which also serves the runtime's own binding for
  generated and `bin` assemblies and refuses `.delete`-marked files.
- Diagnostics: mapped spans resolve to the originating virtual path, and a
  failed compilation is reported on `WebFormsRuntimeEventSource`.
- Failure: top-level compilation runs inside `HostingEnvironment.Initialize`,
  which stashes the exception in `HttpRuntime.InitializationException` and
  renders it per request. The host selects that through `CreateObject`'s
  `throwOnError`, which both hosts now pass as `false`, matching Framework.

Reclamation's contract is logical deletion. Immediate unlink is best-effort;
`.delete` marks an assembly unusable until a later start can sweep it.

## Long literal strings

Decided. Literal markup of 256 characters or more was emitted as a Win32
resource blob and read back by `StringResourceManager.ReadSafeStringResource`
through `GetModuleHandle`, `FindResource`, and `LockResource`, addressing the
memory-mapped module image. That is Windows-only, and it would have failed
during rendering rather than compilation.

`RoslynCSharpCodeProvider` reports `GeneratorSupport.Win32Resources` as absent,
which is the capability check the generator already consults, so literals stay
ordinary metadata strings and no resource file is produced. One branch in
imported source changes behaviour, and none of it is edited.

The accepted deviation is the lost optimization. `HttpWriter.WriteUTF8ResourceString`
copies the resource bytes straight into the response buffer whenever the response
is UTF-8, or the markup is ASCII and the encoding is ASCII-compatible. Metadata
strings are UTF-16, so that memcpy becomes a per-request transcode. The saving is
real and small: transcoding ASCII is vectorized, so a markup-heavy page loses low
single-digit percent of its render, unmeasured here.

`StringResourceManager`, `SafeStringResource`, and
`TemplateControl.ReadStringResource` become unreachable rather than portable.
Recorded as ledger row P39 with treatment **inactive**, and asserted by
`PageCompilationTests`: a compiled page carrying a literal run past the threshold
references none of `WriteUTF8ResourceString`,
`CreateResourceBasedLiteralControl`, or `SetStringResourcePointer`, and its
assembly carries no Win32 resource directory.

### A portable reader is available if the saving is ever worth it

Reading the resource back without Win32 is proven, not speculative. A spike
embedded a blob in the `StringResourceBuilder` format through
`CSharpCompilation.Emit(win32Resources:)` and recovered it byte-identically using
only `PEReader`.

The change is one method, `ReadSafeStringResource`, and roughly forty lines.
`SafeStringResource` is an `(IntPtr, int)` pair, so `ResourceToString`,
`TemplateControl`, `ResourceBasedLiteralControl`, and the `BufferResource` write
path stay unchanged, and the per-request saving returns in full.

Take `PEHeaders.PEHeader.ResourceTableDirectory`, read it through
`GetSectionData`, and walk the three directory levels — type `0xEBB`, id `101`,
then language. Entry offsets are unsigned and the high bit marks a subdirectory,
so reading them as `Int32` makes every valid subdirectory offset look like a
failure. Follow the leaf's data RVA through `GetSectionData` for the bytes.

Costs an imported source edit and therefore a ledger row and a focused test.
Needs the assembly on disk, so the `.delete`-rename fallback needs a portable
answer, and the blob must be copied into memory that outlives the type rather
than pointing into the mapped image. Rendered output is identical either way, so
adopting it later changes no contract.

## Verification

`CodegenSubstrateTests` activates real applications in child processes, because
activation permanently mutates process-global state. It covers top-level
ordering, the generated assembly set including culture satellites, reuse across
restarts, recompilation after an edit, two processes sharing one segment, and
per-platform reclamation. Both supported platforms pass.

Page compilation and its build results pass the
[dynamic ASPX integration](dynamic-aspx-integration.md) gate.

## Open

- User control and master page build providers are registered but no slice
  compiles them yet.
- The enumerated shared framework includes `System.Web.HttpUtility.dll`, so any
  compiled page or `App_Code` line naming `HttpUtility` fails with `CS0433`
  against the port's own type. Framework applications use `HttpUtility`
  routinely; the sample application had to fall back to `Server.HtmlEncode` /
  `WebUtility.HtmlEncode`. The reference enumeration likely needs to exclude
  shared-framework assemblies whose namespaces the port itself supplies.
- Batch compilation settings, satellite culture policy beyond the neutral and
  one-culture case, and `assemblyPostProcessorType` stay with
  [compiler policy](compiler-provider-and-target-framework-policy.md).

## Page compilation resolves framework assemblies over app-local copies

Closed. Found while compiling a page that used `ObjectStateFormatter`.

`RoslynCSharpCompiler` builds the reference set by enumerating the shared
framework directory, standing in for the machine `<compilation><assemblies>` list
a Framework application inherited. When an out-of-band package advances one of
those assemblies, the application loads the higher version while pages saw the
lower one, and any page touching an affected type failed with `CS1705` before it
ran. `System.Runtime.Serialization.Formatters` triggered it: the framework ships
`8.1.0.0`, the package supplies `10.0.0.0`, and `ObjectStateFormatter` carries
`IFormatter` in its public surface.

Framework assembled the set from the assemblies the application had loaded, so
each entry now resolves from the application's deployment directory when a copy
of that name is present there, and from the shared framework otherwise. Only the
version is substituted; the set of visible assemblies is unchanged, so a page
gains no reference it did not already have.

`Formatter.aspx` in the page fixture reproduces it: the assertion is the rendered
`ObjectStateFormatter` output, so the page must compile and run rather than merely
resolve. It is the only assembly of that shape in the repository today — no other
package reference shadows a shared framework assembly.

The substitution matches on file name and does not consult `deps.json`, so an
application deploying an *older* copy of a framework assembly would be compiled
against that copy. Nothing does; revisit if the host resolution rules ever matter
more than the version collision.
