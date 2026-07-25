---
status: accepted
---

# Require strict differential comparison

Core differential probes compare oracle and portable observations exactly after
applying a small checked-in normalization manifest for inherently host-specific
values such as absolute paths, assembly identity, timestamps, and
transport-owned headers.

Every normalization requires a narrow justification. Broad regular-expression
scrubbing is prohibited. Every remaining mismatch must be fixed, recorded as an
accepted compatibility deviation, or classified as unsupported behavior.
