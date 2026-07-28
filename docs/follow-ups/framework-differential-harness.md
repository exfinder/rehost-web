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

## Session model

`prototypes/core-parity/sessions.json` declares what both adapters run, so the
gate's required scenarios are data rather than host code. A session is one
process, one fixture, and an ordered list of steps; a step holds one or more
named requests issued together, so a step of one is sequential. Each adapter
spawns itself once per session and emits one trace covering all of them.

Process-per-session is the contract, not an implementation detail. Cold
activation, single-initialization, and terminal-shutdown claims are only
observable in a process whose `HttpRuntime` singleton and activated application
have never been touched, so a scenario asserting any of them needs its own
session. Warm and pooled claims require the opposite: a later request in a
session that already served a cold one. Scenarios needing different
configuration need their own fixture, and therefore their own session.

Requests are keyed by name in the trace so a divergence reports the request that
diverged instead of shifting every later comparison.

Each request owns an event notebook, and events belonging to the application
rather than to one request — module initialization, the shutdown notification,
the count of application instances created — go to a session notebook.
Concurrent requests would otherwise interleave one shared list and no trace
would reproduce. Probes identify their request from an `X-Parity-Request` header
the recording worker request carries, so recording does not depend on ambient
context while [`CallContext` isolation](illogical-call-context-isolation.md)
remains open.

Done when first-slice fixtures prove capture, narrow normalization, replay,
drift detection, and intentional-deviation documentation.
