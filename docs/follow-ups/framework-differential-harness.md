# .NET Framework differential harness

Status: open. Priority: medium. Depends on runnable fixtures and a Windows .NET
Framework 4.8.1 oracle.

Build a documented oracle workflow that runs equivalent fixtures on .NET
Framework and captures normalized observable expectations. Normalization must
exclude transport/host noise while retaining status, selected headers, body,
lifecycle events, exception shape, and generated-code behavior relevant to the
case.

Golden expectations may be checked in so normal Rehost CI remains Linux-only.
Oracle refresh must be explicit, reproducible, provenance-recorded, and
reviewable.

Done when at least one fixture proves capture, normalization, replay, drift
detection, and intentional-deviation documentation.
