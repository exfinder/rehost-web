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
- Decide how long generated literals and resources are emitted portably.
- Keep compiler/provider selection in
  [compiler-provider-and-target-framework-policy.md](compiler-provider-and-target-framework-policy.md).

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
