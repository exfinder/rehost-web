# Runtime code generation and loading

Status: open. Priority: high. Slice 2 after the precompiled-handler gate.

## Problem

A dynamic `.aspx` request reaches CodeDOM generation, compiler selection,
reference resolution, output cleanup, assembly loading, and cache invalidation.
Modern AppDomain probing, dynamic-directory, and shadow-copy setters are
ineffective. Imported paths also assume Windows separators, native file
enumeration, Win32 resources, and IIS-owned temporary directories.

## Current state

`SetUpCodegenDirectory` now uses configured `codegenBase` (ledger P08). The
precompiled-handler slice writes nothing there.

Before dynamic compilation, define deterministic generation isolation and
reclamation. Generated assemblies cannot unload, while Framework `.delete`
markers require later runs to revisit the same directory. Per-run randomized
paths therefore violate both determinism and reclamation.

## Required decisions

- Use the host-supplied application work root and retained
  `SetUpCodegenDirectory` sequence.
- Decide whether cross-process `CompilationLock` makes a generation segment
  unnecessary; otherwise derive it from stable identity.
- Define ownership, permissions, cleanup, restart, and concurrent compilation.
- Preserve virtual-to-generated source diagnostics.
- Extend the slice-1 application-bin resolver for generated assemblies without
  Framework shadow-copy/unload claims.
- Keep compiler/provider selection in
  [compiler-provider-and-target-framework-policy.md](compiler-provider-and-target-framework-policy.md).

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
Record the ledger row with treatment **inactive** when a slice first compiles a
page, and add the deferred test that a compiled page emits no
`WriteUTF8ResourceString` call.

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

Focused path, containment, cleanup, and loading tests should precede the full
pipeline. Generated page compilation may remain deferred until
[dynamic-aspx-integration.md](dynamic-aspx-integration.md), with the exact
missing test recorded here.

## Done when

Pre-app-start, `App_Code`, `Global.asax`, and `Application_Start` pass their
differential ordering/failure gate. Runtime compilation uses explicit paths,
cleanup cannot escape its disposable root, diagnostics retain useful locations,
and unsupported shadow-copy expectations fail clearly.
