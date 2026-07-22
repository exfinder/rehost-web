# Windows platform diagnostics audit

## Status

Follow-up required. `CA1416` is suppressed for the Runtime project. The exposed
warning inventory previously measured 450 Windows-only API reachability
diagnostics in imported Reference Source.

This suppression is build noise control, not evidence that affected behavior is
safe, unreachable, or supported cross-platform.

## Risk

Imported Runtime code reaches Windows-specific areas including:

- IIS and native process hosting;
- registry and machine configuration;
- COM integration;
- Windows identity, impersonation, and access checks;
- performance counters and process-model services;
- platform-specific filesystem and administration behavior.

Some paths have already been declared unsupported. Others may be reachable from
otherwise supported request, configuration, compilation, caching, or hosting
flows. The project-wide suppression does not distinguish them and also hides new
`CA1416` occurrences.

## Required audit

Temporarily remove `CA1416` from Runtime `NoWarn`, rebuild, and record every
current call site. For each diagnostic:

1. Trace reachability from supported public and hosting entry points.
2. Identify existing runtime platform guards.
3. Classify as portable replacement, Windows-only supported behavior, explicit
   unsupported behavior, or unreachable imported compatibility code.
4. Prefer project-owned overlays or guards over edits made only to silence
   imported source.
5. Use narrowly scoped suppressions only after behavior is documented.

## Completion criteria

- Current `CA1416` inventory recorded by feature and call site.
- Every reachable Windows dependency has an explicit support policy.
- Cross-platform paths fail early with actionable diagnostics or use portable
  behavior.
- Windows-supported paths have correct platform guards.
- Project-wide `CA1416` suppression removed; any remaining suppressions are
  narrow and justified.
- Runtime still builds with warnings treated as errors.
