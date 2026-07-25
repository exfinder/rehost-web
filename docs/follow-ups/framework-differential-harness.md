# .NET Framework differential harness

Status: open. Priority: highest. It precedes the first portable request slice
and requires a Windows .NET Framework 4.8.1 oracle.

Build a documented oracle workflow that runs equivalent fixtures on .NET
Framework and captures normalized observable expectations. Normalization must
exclude transport/host noise while retaining status, selected headers, body,
lifecycle events, exception shape, and generated-code behavior relevant to the
case.

Commit provenance-stamped generated traces. A Windows job regenerates and
verifies them; portable jobs compare on every supported OS. Oracle refresh must
be explicit, reproducible, and reviewable.

Done when first-slice fixtures prove capture, narrow normalization, replay,
drift detection, and intentional-deviation documentation.
