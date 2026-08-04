# Client-reset detection latency

Status: open. Scope: how long a client reset takes to reach the application, and
the fixed wall-clock budget in the scenario harness that turns that latency into
an intermittent Windows failure. Sits on the
[entity-body bridge](request-entity-body-bridge.md).

## What is already settled

A reset used to reach the application as a clean, empty body. `IsClientConnected`
answered from `RequestAborted`, which Kestrel raises *after* the read that
observed the reset, so `HttpBufferlessInputStream.Read` saw a zero-byte read from
a still-connected client and returned end of body. It now answers from the
terminal state the read latched. That is fixed and covered by
`AspNetCoreWorkerRequestTests`; this follow-up is only about *when* the reset is
observed, not what it turns into.

## The remaining problem

`AbortScenario` fails roughly one suite round in eight on the 4-vCPU Windows
host, timing out against the harness's 10s budget in `AbortBodyAsync`. It is a
harness deadline, not a behavioral assertion: the probe polls the trace until the
handler records its outcome.

Adding a fourth concurrent scenario-host class left that rate unchanged — one
failure in eight rounds both with and without it, on the same binaries. So the
trigger is not simply the number of hosts running at once, and a theory resting
on that alone does not fit.

Measured time from client reset to the application observing it:

| | idle | macOS under load | Windows under suite load |
|---|---|---|---|
| synchronous bufferless read | ~47ms | 21ms median, 0.75-3s tail | 0.8s-8.1s |
| worker APM read | ~23ms | 23ms, no spread | 34-50ms |

The APM path is uniform everywhere. The synchronous path is not, and its Windows
tail lands close enough to 10s to decide the round. Whatever the cause, it is
below this port: both paths reach the same `PipeReader`, and only the read sizes
differ — `CopyTo` asks for 81920 bytes, the APM probe for 3.

## What to establish

- Where the synchronous tail comes from. The read-size difference and Kestrel's
  minimum request-body data rate are the two candidates worth eliminating first;
  neither has been tested.
- Whether the tail exists off the test harness at all, or is an artifact of five
  scenario hosts each activating an application on four cores.
- Whether Framework shows comparable spread. The oracle can hold a request open
  after a reset, so this is answerable rather than merely arguable.

## The budget decision

Decided (2026-08-04): the split-claim option. `AbortBodyAsync` asserts the
behavior under a 60s deadline and records the observed detection latency in the
trace as `body[-apm]-abort-latency-ms`, never asserting on it.

Nothing here should serialize the scenario classes. That was tried, and it
converted a genuine defect into what looked like a scheduling artifact.

## win-oracle findings (2026-08-04)

On the 4-vCPU EC2 host the failure is no longer a tail — under the full
Hosting.Tests suite the reset is **never observed within 60s**, deterministically
(three consecutive rounds, pre- and post-P1-restructure binaries, so the test
suite refactoring is exonerated by bisect). Every reduced configuration on the
same box passes in milliseconds:

| configuration | result |
|---|---|
| single host, one abort probe (either path) | 21-32ms |
| single host, both probes (the `AbortScenario` shape) | 26-32ms |
| `RequestBodyOverKestrelTests` class alone (7 hosts) | 11/11 pass |
| full Hosting.Tests suite (all scenario classes) | never detected, 60s timeout |

A 60s+ non-detection is not latency; it looks like the reset notification being
lost outright under cross-class host concurrency and only a later stimulus would
surface it. This narrows "where the synchronous tail comes from" above: the
mechanism must be able to lose the event entirely, not merely delay it. Both
sync and APM probes fail together in the suite context, unlike the earlier
tail data where APM stayed uniform — establish which probe actually times out
first in that context (the first probe's failure aborts the host before the
second runs).

## Reproducing

Five concurrent scenario hosts are enough; the failure needs process contention,
not a small CPU count. `DOTNET_PROCESSOR_COUNT=2` alone reproduces nothing.

```text
6 hosts x 200 aborts, alternating --body-probe abort / abort-apm
```

On a hung host, `dotnet-stack report --process-id <pid>` distinguishes a blocked
read from a request that already returned; that distinction is what disproved
thread-pool starvation as the cause of the original defect.
