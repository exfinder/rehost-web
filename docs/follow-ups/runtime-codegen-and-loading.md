# Runtime code generation and loading

Status: open. Priority: high. Slice 2 after the precompiled-handler gate.

## Problem

A dynamic `.aspx` request reaches CodeDOM generation, compiler selection,
reference resolution, output cleanup, assembly loading, and cache invalidation.
Modern AppDomain probing, dynamic-directory, and shadow-copy setters are
ineffective. Imported paths also assume Windows separators, native file
enumeration, Win32 resources, and IIS-owned temporary directories.

## Current state

`SetUpCodegenDirectory` completes portably (ledger P08). `AppDomain.SetDynamicBase`
is a no-op and `DynamicDirectory` is null on modern .NET, so no generation segment
stands in for the one the CLR supplied:

```csharp
_codegenDir = codegenBase;
```

`codegenBase` is derived from the configured `compilation/tempDirectory` and the
application name, as on Framework. The path is a pure function of configuration,
so it is identical across runs and machines.

The sibling POC instead appends `HashCode.Combine(Environment.ProcessId,
AppDomain.CurrentDomain.Id)`. That was tried and rejected: `HashCode` seeds from
`Interop.GetRandomBytes` once per process, so the segment is not a function of
the pid at all — the same application produces a different directory on every
run. Beyond violating the determinism contract, a per-run directory cannot be
reused or reclaimed.

The hazard the POC's segment was reaching for is real and is deferred with the
rest of dynamic compilation:

- Generated assemblies stay loaded for the life of the process on .NET 10, since
  there is no AppDomain unload (ledger P26). Recompiling a changed page can
  therefore collide with an older, locked assembly of the same name in a shared
  directory.
- Framework's answer is the `.delete`-marker design
  (`BuildResultCache.TryDeleteFile` / `CheckAndRemoveDotDeleteFile`): when
  deletion fails because the assembly is locked, it leaves a marker and reclaims
  the file on a later pass over the *same* directory. Any generation segment that
  changes per run defeats that reclamation outright.

Nothing writes to this directory while the supported fixture uses a precompiled
handler, so the collision cannot occur yet. It must be resolved before dynamic
compilation is enabled; H10 stays partially met until then.

Outstanding, low priority while the supported fixture uses a precompiled handler
and writes nothing to this directory:

- decide whether a generation segment is required at all, given one application
  per process and the cross-process `CompilationLock` mutex that already
  serializes access to a shared `tempDirectory`;
- if it is, prefer an identity-derived deterministic segment plus an explicit
  reclamation step that runs at startup, so that reuse, collision safety, and
  cleanup all hold;
- define the conflict and work-root probes H10 asks for before choosing.

## Required decisions

- Use the host-supplied application work root and retained
  `SetUpCodegenDirectory` sequence.
- Define ownership, permissions, cleanup, restart, and concurrent compilation.
- Preserve virtual-to-generated source diagnostics.
- Extend the slice-1 application-bin resolver for generated assemblies without
  Framework shadow-copy/unload claims.
- Decide how long generated literals and resources are emitted portably.
- Keep compiler/provider selection in
  [compiler-provider-and-target-framework-policy.md](compiler-provider-and-target-framework-policy.md).

POC implementations are hazard evidence only. Reanalyze every replacement
against Rehost contracts.

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
