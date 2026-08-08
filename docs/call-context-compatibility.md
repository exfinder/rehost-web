# CallContext compatibility

System.Web restores its internal
`System.Runtime.Remoting.Messaging.CallContext` use without restoring remoting.

- Logical values flow with `ExecutionContext`.
- Immutable snapshots prevent child-task writes leaking to parents.
- Illogical values remain local and clear on execution-context transitions.
- Illogical isolation is approximate, and fails open. A pooled thread that both
  suspends and resumes the same illogical state reads the resumption as
  inheritance, so a work item scheduled onto that thread can observe the values of
  the one before it. The visible symptom is `HttpContext.Current` — it reaches
  `CallContext.HostContext` through `ContextBase` — appearing non-null inside a
  `Task.Run` where .NET Framework guarantees null. Framework kept illogical data in
  the `ExecutionContext` and simply did not copy it on capture; `AsyncLocal` has no
  such mode and reports no transition kind, so the suspended-state stack is the
  only available approximation. Thread ownership is not a substitute: illogical
  data is execution-context-scoped, not thread-scoped, and clears across
  `ExecutionContext.Run` on the same thread. Tracked in
  [illogical call context isolation](follow-ups/illogical-call-context-isolation.md),
  required before the first asynchronous pipeline scenario.
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
`src/Rehost.WebForms.Runtime/Compatibility/Remoting/CallContext.cs`.
Tests: `tests/Rehost.WebForms.Runtime.Tests/CallContextTests.cs`.
