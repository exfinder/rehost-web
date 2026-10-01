# Windows platform diagnostics

## Problem

Runtime-wide `CA1416` suppression hides Windows-only calls involving IIS/native
hosting, registry/configuration, COM, identity/impersonation, performance
counters, and filesystem/administration behavior. Suppression is not evidence
of safety or unreachability.

## Required audit

Rebuild without the suppression. For each call, trace reachability and classify
it as portable replacement, explicit unsupported behavior, platform-guarded
tooling, or unreachable imported code. Prefer overlays/guards; use narrow
suppressions only after the contract is documented.

The first-request stories classify their reachable subset. This story owns the
repository-wide audit and verifies that no supported behavior works only on
Windows. POC guards, no-ops, and replacements are leads for investigation, not
accepted solutions.

## Done when

- Every reachable Windows dependency has an explicit support policy.
- Cross-platform paths use portable behavior or fail early.
- The compatibility feature map names every explicitly excluded Windows/IIS
  feature and its diagnostic.
- Project-wide suppression is removed; remaining suppressions are narrow.
- Runtime still builds with warnings treated as errors.
