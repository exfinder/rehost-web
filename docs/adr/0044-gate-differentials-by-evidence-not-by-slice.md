---
status: accepted
---

# Gate differentials by evidence, not by slice

[ADR 0040](0040-port-in-vertical-semantic-slices.md) required a differential gate
per slice. That cadence is replaced: a Framework differential fixture is proposed
where it can decide something, and port-local tests on both supported platforms
are the gate everywhere else.

The port copies System.Web nearly verbatim and repairs incompatible leaves one
line at a time. Structure, control flow, and statement conditions stay as
imported. A differential over the output of unmodified code mostly re-asserts
code the port never touched, at the cost of a Windows oracle round trip for every
fixture change. [ADR 0043](0043-gate-the-compilation-substrate-locally.md) already
reached that conclusion for one slice; it generalizes.

A differential is worth proposing when:

- the port replaces native or host-owned code, so only Framework can say what the
  replacement should produce; or
- the port must match a format it cannot derive from its own inputs.

Neither holds merely because a slice renders something new. Where the expected
output follows from the input — literal markup renders as the markup — a
port-local expectation encodes no Framework implementation detail and
[ADR 0030](0030-commit-reproducible-oracle-traces.md) is not in tension.

The first-slice golden remains a standing regression gate. It replaced native and
host-owned code, which is exactly the case above, and `PortableParity.Host verify`
and `AdapterParity.Host verify` must keep matching it exactly on every change.
[ADR 0031](0031-require-the-first-slice-parity-gate.md) stands unchanged.

Slice 3 takes port-local tests on both platforms. ADR 0043 deferred slice 2's
ordering ratification to it; that debt is closed as no longer owed rather than
carried forward, because the ordering is decided by imported source and is
already asserted port-locally.
