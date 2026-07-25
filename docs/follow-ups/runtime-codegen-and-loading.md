# Runtime code generation and loading

Status: open. Priority: high. Depends on host/runtime filesystem contract.

## Problem

A dynamic `.aspx` request reaches CodeDOM generation, compiler selection,
reference resolution, output cleanup, assembly loading, and cache invalidation.
Modern AppDomain probing, dynamic-directory, and shadow-copy setters are
ineffective. Imported paths also assume Windows separators, native file
enumeration, Win32 resources, and IIS-owned temporary directories.

## Required decisions

- Define host-supplied, application-scoped codegen and cache roots.
- Define ownership, permissions, cleanup, restart, and concurrent compilation.
- Preserve virtual-to-generated source diagnostics.
- Define assembly probing/loading without Framework shadow-copy claims.
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

Runtime compilation uses explicit paths, cleanup cannot escape its disposable
root, location/probing behavior is tested, compiler diagnostics retain useful
locations, and unsupported shadow-copy expectations fail clearly.
