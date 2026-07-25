---
status: accepted
---

# Preserve Framework semantics within the compatibility envelope

Observed .NET Framework behavior wins within the declared compatibility
envelope, including awkward ordering and failure behavior. Rehost deviates only
when the behavior depends on excluded IIS, Windows, or secondary-AppDomain
machinery, or conflicts with an explicit safety constraint.

Each deviation is an explicit compatibility contract backed by a differential
test; portability work must not silently “improve” legacy semantics.
