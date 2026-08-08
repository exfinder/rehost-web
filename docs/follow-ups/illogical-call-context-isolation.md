# Illogical CallContext isolation and HttpContext.Current

Status: open. Priority: required before the delayed-asynchronous scenario of
[ADR 0031](../adr/0031-require-the-first-slice-parity-gate.md).

## Background

.NET Framework kept illogical call context data in the `ExecutionContext` and
did not copy it on capture. That single property gives both behaviors System.Web
depends on: values survive synchronous calls on the same thread, and they vanish
in any flowed continuation. `ExecutionContext.Run` clears them on the same
thread, so the storage is execution-context-scoped, not thread-scoped.

`AsyncLocal<T>` has no non-flowing mode, and
`AsyncLocalValueChangedArgs` reports only that the thread context changed — not
whether the change is a child inheriting a captured context or a thread resuming
one it previously suspended. The port therefore reconstructs the distinction with
a `[ThreadStatic]` stack of suspended states in
`CallContext.OnIllogicalContextChanged`: a value matching the top of the stack is
treated as a resumption and kept, anything else is treated as inheritance and
cleared.

## The defect

Reference equality cannot separate the two cases. A pooled thread that suspends
state `S` and later receives `S` again by inheritance pops it and keeps it, so a
work item observes the illogical values of the one before it. Pool-thread reuse
across work items of a single request is ordinary ASP.NET scheduling, not an
exotic interleaving.

The consumer-visible symptom is `HttpContext.Current`, which reaches
`CallContext.HostContext` through `System.Web.Hosting.ContextBase`: it can appear
non-null inside a `Task.Run` where Framework guarantees null. The failure is
nondeterministic and fails open, so dependent code appears correct and then
null-references under different scheduling. Non-affinative `SetData` slots share
the defect — `BuildManager`'s circular-reference and batch-compilation checkers,
`AspCompat`, `HostingEnvironment` temporary path mappings, and the `WebEvents`
re-entrancy guard.

It surfaced as a roughly one-in-six flake in
`CallContextTests.HostContext_flows_only_for_affinative_values`. That test now
uses a dedicated thread, which makes it deterministic and leaves the defect
untested.

## Reachability

The async-pages story (ledger P63) made flowed illogical state hot: every
truly-pending await now wipes and — on request threads — restores through the
`CallContext` restore hook. The oracle-pinned scenarios pass, and the restore
path narrows the symptom's surface for `HttpContext.Current` on request-serving
threads, but the suspended-state heuristic itself is unchanged: pool-thread
reuse can still pop a stale matching state, and the non-affinative `SetData`
slots still ride the heuristic alone. The story also made `HostContext`
publication immutable (in-place mutation rewrote states captured
`ExecutionContext`s still referenced), which removes one aliasing hazard the
heuristic previously interacted with.

## Rejected: thread-owned illogical state

Stamping each `IllogicalState` with its owning thread and ignoring foreign states
is deterministic, but wrong. It would make illogical data survive
`ExecutionContext.Run` on the same thread, which
`CallContextTests.Parent_illogical_data_is_restored_after_child_execution_context`
pins as Framework behavior. Thread affinity is a different contract from the one
Framework actually had.

## Directions worth evaluating

- Distinguish suspension from inheritance by identity rather than value: suspend
  a fresh token per transition so a repeat arrival of the same state cannot
  match. Requires establishing that the token's lifetime is bounded.
- Give `HttpContext.Current` dedicated storage with explicit pipeline-owned set
  and clear, bypassing `CallContext` for the one slot that matters. Fixes the
  visible symptom, diverges from the imported `ContextBase` indirection, and
  leaves the other illogical slots on the heuristic.
- Establish whether any reached path can observe the difference at all, and if
  none can, narrow the supported contract explicitly rather than approximating a
  guarantee the port cannot make.

Prefer whichever reproduces Framework's execution-context-scoped,
non-flowing semantics directly, over anything that approximates its effects.

## Done when

Illogical isolation holds deterministically across pool-thread reuse, proven by
a test that fails against the current suspended-state stack, with the seven
existing `CallContextTests` unchanged — they encode the Framework semantics any
replacement must keep.

See [CallContext compatibility](../call-context-compatibility.md).
