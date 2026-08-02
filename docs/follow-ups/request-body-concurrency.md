# Request-body concurrency measurement

Status: open. Scope: locate the throughput cliff created by synchronous
`ReadEntityBody` blocking a pooled worker, and decide from numbers whether the
threading model needs to change.

## Why

`RehostWebFormsMiddleware` runs `ProcessRequest` inline on the ASP.NET Core
request thread. A synchronous entity read blocks that pooled worker awaiting a
`PipeReader` completion that Kestrel schedules back onto the same pool, so
blocked threads delay the very continuations that would release them. The
reasoning, the bounds, and the rejected alternative are recorded in
[ADR 0037](../adr/0037-do-not-assume-kestrel-synchronous-io.md).

Nothing measures it. Every existing body test is single-request, so the
behavior under concurrent slow uploads is unobserved rather than known-good.

## What to measure

- Concurrency at which median request latency for an unrelated body-less request
  departs from its idle baseline. That is the cliff, and it is the number that
  decides whether the current model is adequate.
- Time to recover once the slow uploads stop, which is governed by thread
  injection rather than by anything this port controls.
- The same two figures with `asyncPreloadMode="All"`, to confirm the documented
  opt-out actually removes the effect rather than moving it.
- Sensitivity to `ThreadPool.MinThreads`, to establish whether an operator can
  configure the cliff away without a code change.

Drive it with bodies that clear `MinRequestBodyDataRate` — a slower client is
aborted by Kestrel and never reaches the interesting state.

## Decision this feeds

Whether to keep inline activation, or move `ProcessRequest` for body-carrying
requests onto a thread outside Kestrel's pool. The latter is only worth its
costs — a dedicated OS thread per such request, and thread-affinity assumptions
no test currently covers — if the cliff arrives at a concurrency a real
deployment would reach.

Run on both supported platforms; thread injection and pool sizing are not
identical across them.
