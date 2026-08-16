# Connection abort at request timeout

`executionTimeout` is currently cooperative: it cancels
`Request.TimedOutToken`, flags the request, and unwinds at the next pipeline-step
boundary. A synchronous step that never returns cannot be stopped safely on
modern .NET.

Bounding the client's wait requires the hosting layer to abort the connection at
the budget while leaving the server-side step to finish or remain stuck.
Framework used the same transport escape when timeout occurred during a
synchronous entity-body read.

## Design decision: bound the client and recycle, do not kill the thread

A stuck request must never be answered by terminating its thread. This is a
deliberate boundary, not a gap to close later:

- **No mechanism.** `Thread.Abort` is gone on modern .NET
  (`PlatformNotSupportedException`); there is no supported way to force-unwind a
  managed thread. `Thread.Interrupt` is safer than Abort — it throws only at a
  *managed* blocking wait (`Monitor.Wait`/`Sleep`/`WaitOne`), not at an arbitrary
  instruction — but it is still unsafe as a general kill and unreliable besides:
  a latched interrupt can surface at an unrelated later wait deep in a library,
  it hands the wait site a `ThreadInterruptedException` almost no code is written
  to handle (the awaited condition is left unmet), and it does nothing to a
  CPU-spin, a native/P-Invoke block, or code that catches it. It is a
  cooperative wake-up for a thread you own that opts into interruption, not a way
  to terminate arbitrary application code.
- **Unsafe by design.** Aborting mid-execution leaves locks held, `finally`
  blocks unrun, and shared state half-mutated. Framework tolerated this only
  because it could recycle the AppDomain to contain the damage. This runtime is
  one application per OS process in the current AppDomain (see
  [`PROJECT.md`](../../PROJECT.md)); there is no AppDomain boundary to quarantine
  a corrupt-after-abort state, so a forced abort would poison every other
  request in the process.

Reclamation is therefore process-level, staged as an escalation ladder:

1. **Cooperative cancel (present).** `TimedOutToken` + unwind at step
   boundaries. The only class reclaimed cleanly — async code that honors the
   token.
2. **Connection abort — bound the client (this follow-up).** Free the socket at
   the budget so the client never waits on a wedged server; the server-side step
   may stay stuck. Abort only unwinds a thread blocked *in* the aborted I/O; a
   thread stuck on a lock or a continuation-scheduling cycle (see the
   sync-over-async flush deadlock, ledger P79) will not return regardless.
3. **Account the stuck thread as leaked.** It cannot be killed, but it can be
   counted: a request past a hard budget that the abort did not free is a
   permanently consumed pool thread (its stack, plus any locks it holds).
4. **Process recycle — terminal escalation.** When leaked threads accumulate
   past a threshold, replace the process; the OS reclaims the threads. This is
   the only genuine recovery for a real application deadlock and is exactly what
   IIS did (deadlock detection → worker-process recycle). Process replacement is
   already this project's restart/isolation model.

The reclaimable surface is the async paths the runtime controls: the more
`RequestAborted`/`TimedOutToken` is threaded through them, the smaller the
"only a recycle can save it" zone. It cannot be eliminated for classic
synchronous Web Forms, but it can be kept from growing.

## Required work

- Define the owner and timing of connection abort relative to response seal,
  final flush, and `RequestAborted`.
- Cover abort racing normal completion, error formatting, and host shutdown.
- Preserve the cooperative 500 when the step returns before transport abort.
- Ensure the server does not present a partially committed response as success.
- Account requests still stuck after abort as leaked threads, and feed that
  count into the recycle policy ([runtime process
  policy](runtime-process-policy.md), [process
  lifetime](process-lifetime-shutdown-and-recycle.md)).

## Done when

A never-returning synchronous step cannot hold the client connection beyond the
declared policy, without pretending the server-side work was terminated; and
accumulated leaked threads drive a process recycle rather than a silent pool
bleed.
