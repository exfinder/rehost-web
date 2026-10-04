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
  bounding retention by in-flight flows rather than request history.
- `ILogicalThreadAffinative` values and host contexts flow.
- `HostContext` survives await resumptions through a restore seam:
  the wipe path raises a neutral hook, and System.Web's registered handler
  re-establishes the context on threads whose associated `ThreadContext` serves
  the same request — Framework's `AspNetHostExecutionContextManager` guard,
  rebuilt at the only observation point the modern CLR leaves. `HostContext`
  changes publish a replacement state object: captured `ExecutionContext`s alias
  the published state, so in-place mutation would rewrite what an in-flight
  await captured.
- Remoting headers, principals, serialization, and internal context swapping
  are omitted because retained System.Web paths do not use them.

The implementation derives from Reference Source callcontext.cs; the import
identity is in [sources](sources.md). Types remain internal;
publishing them would imply unsupported general remoting compatibility.

Implementation:
`src/Rehost.Web/Compatibility/Remoting/LegacyCallContext.cs`, as `System.Web.Util.LegacyCallContext`;
imported code reaches it through the `CallContext` alias in `RemotingAliases.cs`, so
no type of the Framework name exists and application code naming it gets the
"namespace does not exist" error rather than an accessibility one.

An ExecutionContext containing only illogical HostContext may be treated as a
default context by Framework. Isolation fixtures need the SynchronizationContext
an ASP.NET request carries to exercise a context transition.
