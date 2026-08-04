---
status: accepted
---

# Commit reproducible oracle traces

Commit the .NET Framework 4.8.1 oracle harness source and its
provenance-stamped generated traces. A Windows job regenerates and verifies
those traces against the pinned Framework environment.

Portable jobs compare observations with the committed oracle traces on every
supported operating system. Oracle traces are generated evidence, never
hand-authored expectations. Mechanical reformatting of a committed trace
(whitespace and indentation, proven value-identical) is not hand-authoring;
the values themselves remain generated-only, and comparison is by parsed
tree, not bytes.
