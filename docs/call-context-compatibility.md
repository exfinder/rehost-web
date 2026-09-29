# CallContext compatibility

System.Web restores its internal
`System.Runtime.Remoting.Messaging.CallContext` use without restoring remoting.

- Logical values flow with `ExecutionContext`.
- Immutable snapshots prevent child-task writes leaking to parents.
- Illogical values remain local and clear on execution-context transitions.
- Illogical isolation is exact for every path the port runs, and has one
  stated boundary. `AsyncLocal` reports "the thread's context changed" for both
  a return from a nested `ExecutionContext.Run` and a captured context entering
  `Run`; nothing distinguishes them when the captured context is the origin
  thread's own. The port therefore treats a returning suspended state as a
  resumption. Consequence: a continuation captured while a scope still held
  illogical data is visible if it lands back on the thread that captured it —
  Framework hides it. Every other case is Framework-exact: other threads, other
  requests' captures, nested `Run` on the same thread (hidden, then restored),
  and data cleared before the thread left. The boundary is unreachable through
  System.Web: `HttpContext.Current` is bracketed by `ThreadContext`, the
  `WebEvents`, `TemplatedMailWebEventProvider`, and `HostingEnvironment` slots
  by `try/finally`, and the three checker slots that stay set
  (`TimeStampChecker`, `BuildManager`'s circular-reference and batch checkers)
  hold only the request's own scratch objects, so the worst case is a request
  seeing its own empty checker again. `CallContext` is internal, so no other
  caller exists. Suspended states are held weakly and pruned as their flows die,
  bounding retention by in-flight flows rather than request history. The
  contract suite (below) keeps the Framework reading of the boundary in
  `FrameworkOnlyIsolationTests`, compiled into the net481 leg only. On the
  real thread pool Framework is not absolute either: mscorlib 4.8.9337 showed a
  same-scope value to its own `ConfigureAwait(false)` continuation 1-2 times per
  4000 flows in some runs (winbox, 2026-08-15), the same order as the port; the
  suite bounds that rate rather than asserting zero on either leg.
- `ILogicalThreadAffinative` values and host contexts flow.
- `HostContext` survives await resumptions through a restore seam (ledger P63):
  the wipe path raises a neutral hook, and System.Web's registered handler
  re-establishes the context on threads whose associated `ThreadContext` serves
  the same request — Framework's `AspNetHostExecutionContextManager` guard,
  rebuilt at the only observation point the modern CLR leaves. `HostContext`
  changes publish a replacement state object: captured `ExecutionContext`s alias
  the published state, so in-place mutation would rewrite what an in-flight
  await captured.
- Remoting headers, principals, serialization, and internal context swapping
  are omitted because retained System.Web paths do not use them.

The implementation derives from Microsoft Reference Source commit
`ec9fa9ae770d522a5b5f0607898044b7478574a3`,
`mscorlib/system/runtime/remoting/callcontext.cs`. Types remain internal;
publishing them would imply unsupported general remoting compatibility.

Implementation:
`src/Rehost.Web/Compatibility/Remoting/CallContext.cs`.
Tests: `tests/Rehost.Web.Tests/CallContextTests.cs`.
Contract suite: `tests/Rehost.Web.CallContext.Contract.Tests` runs the same
bodies against mscorlib (net481, Windows round only) and the port (net10.0); the
Framework leg is the authority for illogical isolation. Note that mscorlib's
`ExecutionContext.IsDefaultFTContext` ignores a lone illogical `HostContext`, so
`Run` does not switch a thread carrying nothing else — pinned there as
`Bare_HostContext_...`; the isolation tests install a `SynchronizationContext`
as every ASP.NET request thread has.
