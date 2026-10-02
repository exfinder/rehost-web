# Session state: state server mode

Split from [session state](session-state.md). The imported client reaches transport
only through native SessionNDConnectToService/MakeRequest/FreeBody; the native shim
refuses. A reachable service alone cannot establish support.

## Unknown wire contract

StateWorkerRequest is a server adapter over already-parsed values, not a protocol
implementation. Native webengine4 adds status framing, content length and the body.
The source exposes GET/PUT/HEAD/DELETE and exclusive-acquire, timeout, lock-cookie,
lock-date/age and flags semantics; CGI-style names do not establish wire spellings.

Capture the Framework client's request against a local byte listener before
selecting transport. This does not need a state service. Then determine whether
responses have a standard HTTP status prefix, Content-Length, Host tolerance and
connection semantics that HttpClient supports, or require raw sockets.

## Transport choices

- Replace native calls inside OutOfProcStateClientManager, keeping imported retry,
  partitioning, lock-cookie and exclusive-acquire behavior. This is the narrowest
  seam but requires surgical imported-source edits.
- Add a SessionStateStoreProviderBase implementation. The mode switch directly
  constructs the original provider, so wiring still changes imported code and
  locking behavior would be reimplemented.
- Implement selected SessionND methods in the native shim. This weakens its
  categorical refusal of unsupported native operations and needs an explicit
  boundary decision.

Mono's StateServer uses remoting and its own service rather than Microsoft's
protocol; its SQL session schema also differs from ASPState. Neither supplies an
interoperable transport shortcut.

## Service and completion

End-to-end validation needs aspnet_state.exe on a Windows machine, even with a
portable client. Verify service availability and configuration before provisioning;
any durable host changes need separate authorization.

Done when the wire contract selects a transport, executable tests cover the chosen
seam and a StateServer application reaches the real service. Replace the preflight
refusal only after stating the Windows-service deployment dependency.
