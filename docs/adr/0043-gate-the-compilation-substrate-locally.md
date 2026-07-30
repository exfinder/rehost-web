---
status: accepted
---

# Gate the compilation substrate locally

[ADR 0040](0040-port-in-vertical-semantic-slices.md) required a differential gate
per slice. Slice 2 takes port-local tests instead, on both supported platforms.

Slice 1 replaced native and host-owned code, so only a Framework golden could
establish that the replacement behaved identically. Slice 2's sequencing —
pre-application start, `App_Code`, `Global.asax`, `Application_Start` — is
decided by imported Reference Source executed verbatim. A golden there mostly
re-asserts that code we did not change still behaves as it did.

What slice 2 can get wrong is invisible to the differential. Load contexts,
codegen path derivation, cross-process locking, `.delete` sweeping, and
reuse-versus-wipe leave no trace in an observation the two runtimes can compare:
Framework's codegen directory carries `csc` artifacts an in-process Roslyn
compiler never writes, and generated assembly names carry a random suffix. Making
them comparable would require normalization rules that encode Framework
implementation details as contract, against
[ADR 0029](0029-require-strict-differential-comparison.md), which keeps the
normalization manifest empty.

Port-local tests assert that ordering now.

The deferral to slice 3 named here is withdrawn by
[ADR 0044](0044-gate-differentials-by-evidence-not-by-slice.md), which
generalizes this ADR's reasoning: ordering decided by imported source running
verbatim is not something a differential decides. Slice 3 is gated port-locally
too, and the debt is closed as no longer owed rather than carried forward.

The slice-1 golden remains a regression gate throughout: every change here must
keep `PortableParity.Host verify` matching exactly.
