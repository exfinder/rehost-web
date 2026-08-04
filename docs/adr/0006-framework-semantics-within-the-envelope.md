---
status: amended by 0044 and 0045
---

# Preserve Framework semantics within the compatibility envelope

> Amended: the per-deviation differential-test clause is superseded by
> [ADR 0044](0044-gate-differentials-by-evidence-not-by-slice.md)'s evidence
> rule; see [ADR 0045](0045-test-architecture-after-the-suite-refactoring.md).
> The semantics-preservation rule itself stands.

Observed .NET Framework behavior wins within the declared compatibility
envelope, including awkward ordering and failure behavior. Rehost deviates only
when the behavior depends on excluded IIS, Windows, or secondary-AppDomain
machinery, or conflicts with an explicit safety constraint.

Each deviation is an explicit compatibility contract backed by a differential
test; portability work must not silently “improve” legacy semantics.
