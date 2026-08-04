# Client-reset detection latency

Status: resolved (2026-08-05). The "lost reset" was never a runtime defect;
reset detection worked throughout. The failure lived in the scenario harness's
trace-file channel.

## Root cause

All scenario evidence meets in one trace file. Probe handlers append to it;
the abort probe's client polled it with `File.ReadAllText`, which on Windows
opens the file sharing reads but not writes. For the duration of each poll, a
concurrent journal append threw `IOException` ("being used by another
process"). When the collision hit the probe's outcome marker, three things
cascaded: the marker was lost (no retry), the `IOException` escaped the
probe's `HttpException`-only catch and turned the observed request into a 500,
and the client polled for a marker that could never arrive until its budget
expired — indistinguishable from "the server never observed the abort".

Every earlier observation follows: load-dependence (more journal traffic and
slower polls, more collisions), the bimodal shape (detection was always
~30 ms; the coin-flip was the marker write), the kernel connection being gone
during "hangs" (the request had already 500'd), and instrumentation vanishing
on failing runs (debug writes used the same file). Kestrel's own logs confirm
the healthy path end to end: the client's linger-RST arrives as a FIN on
Windows loopback, the pending body read completes with
`BadHttpRequestException`, and the coordinator latches it into `HttpException`
in 21–62 ms, every run.

## The fix

`TraceJournal.Record` opens for append with `FileShare.ReadWrite | Delete` and
retries briefly on `IOException`; pollers read through `TraceJournal.ReadAll`
with the same sharing. `TraceJournalTests` hammers a concurrent reader/writer
pair. Confirmed on the 4-vCPU Windows host with a load harness that
reproduced the failure at 40–75% before the fix: 0/24 afterwards, all
detections 7–117 ms.

## Residual notes

- The abort scenarios now act in the test over a raw socket and observe the
  outcome through the fixture's in-memory witness endpoint; detection latency
  is written to test output, never asserted. The trace-polling loop that
  carried this bug is deleted.
- The old "0.8–8.1 s sync tail" table conflated genuine scheduling latency
  with marker-write collisions; treat it as superseded by the figures above.
- The witness pattern (plan P5) retires file-based evidence for in-lifetime
  facts entirely, which removes this failure class by construction — one more
  reason to migrate the remaining journal consumers.
