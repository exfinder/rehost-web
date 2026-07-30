---
status: accepted
---

# Gate the compilation substrate locally

[ADR 0040](0040-port-in-vertical-semantic-slices.md) requires a differential gate
per slice. Slice 2 takes port-local tests instead, on both supported platforms,
and its ordering is ratified by slice 3.

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

Detection is deferred, not abandoned. Slice 3's `.aspx` fixture cannot render
without this machinery, so its differential ratifies the ordering one slice
later. Port-local tests assert that ordering now, so a slice-3 mismatch has a
known-good local expectation to compare against.

The slice-1 golden remains a regression gate throughout: every change here must
keep `PortableParity.Host verify` matching exactly.
