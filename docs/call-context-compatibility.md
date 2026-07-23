# CallContext compatibility

System.Web restores its internal
`System.Runtime.Remoting.Messaging.CallContext` use without restoring remoting.

- Logical values flow with `ExecutionContext`.
- Immutable snapshots prevent child-task writes leaking to parents.
- Illogical values remain local and clear on execution-context transitions.
- `ILogicalThreadAffinative` values and host contexts flow.
- Remoting headers, principals, serialization, and internal context swapping
  are omitted because retained System.Web paths do not use them.

The implementation derives from Microsoft Reference Source commit
`ec9fa9ae770d522a5b5f0607898044b7478574a3`,
`mscorlib/system/runtime/remoting/callcontext.cs`. Types remain internal;
publishing them would imply unsupported general remoting compatibility.

Implementation:
`src/Rehost.WebForms.Runtime/Compatibility/Remoting/CallContext.cs`.
Tests: `tests/Rehost.WebForms.Runtime.Tests/CallContextTests.cs`.
