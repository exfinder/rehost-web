# CallContext compatibility

Restored the `System.Runtime.Remoting.Messaging.CallContext` surface used by
System.Web without restoring remoting. The implementation preserves separate
logical and illogical stores: logical values flow with `ExecutionContext`;
copy-on-write state prevents child writes leaking to a parent; ordinary values
remain local; `ILogicalThreadAffinative` values and host contexts flow.

Provenance: Microsoft Reference Source commit
`ec9fa9ae770d522a5b5f0607898044b7478574a3`,
`mscorlib/system/runtime/remoting/callcontext.cs`, SHA-256
`32199a20e96885c788196ce04dbf9fcae7017fbce60b18f627d9776a742c8d7e`.
The POC `AsyncLocal<ConcurrentDictionary>` implementation was rejected because
it made ordinary values logical and allowed child dictionary mutations to leak.

Deviation: modern .NET exposes no mutable `ExecutionContext` call-context
stores. Illogical state is cleared on execution-context transitions by an
`AsyncLocal` notification; logical state uses immutable snapshots. Remoting
headers, principals, serialization, and internal context swapping are omitted:
no scoped System.Web call site uses them.

Both compatibility types are internal. Publishing them from `System.Web.dll`
would create the wrong assembly identity and imply unsupported general-purpose
remoting compatibility.

Validation: focused xUnit v3 + Shouldly tests cover task/thread flow, child-write
isolation, marker dispatch, slot removal, and host-context dispatch. Mandated
runtime build moves 27 errors to 14; warnings remain 1,083. Remaining errors are
AppDomain/ObjRef, EnterpriseServices, COM, XSD, and serialization groups.
