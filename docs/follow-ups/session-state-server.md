# Session state: state server mode

Split from [session state](session-state.md).

## The blocker

There is no client. `State/OutOfProcStateClientManager.cs` reaches the network
exclusively through native entry points — `SessionNDConnectToService`
(`:171`), `SessionNDMakeRequest` (`:319`), `SessionNDFreeBody` — and
`UnsafeNativeMethods`'s static constructor throws
`PlatformNotSupportedException` on any use (`UnsafeNativeMethods.cs:27-30`,
ledger P41). Standing up a state server and reaching it over a tunnel proves
nothing until a managed transport exists.

`State/StateRuntime.cs` and `State/StateWorkerRequest.cs` are the *server* side
— the implementation the Windows service hosts — and are native too
(`STWNDSendResponse`, `STWNDDeleteStateItem`).

## The wire format is not in this repository

This corrects an assumption worth not repeating: `StateWorkerRequest.cs` looks
like it documents the protocol, and it does not.

- It never sees the wire. It is an `HttpWorkerRequest` adapter over values that
  arrive **already parsed** — `uri`, `exclusive`, `timeout`, `lockCookie`,
  `contentLength` are constructor parameters (`:56-99`). The parsing is native.
- `SendStatus` builds `"200 OK\r\n"` with **no HTTP version prefix**; whatever
  precedes it is added by native `STWNDSendResponse` (`:345-348`).
- `SendCalculatedContentLength` deliberately does nothing — *"we append the
  content-length in STWNDSendResponse"* (`:367-371`).
- `SendResponseFromMemory` for a 200 GET receives **a pointer to unmanaged
  memory**, `length == IntPtr.Size`; the session bytes are transmitted by native
  code (`:399-410`).

What the imported source does yield: the verb set (GET, PUT, HEAD, DELETE) and
the header semantics. `StateRuntime.cs:197-213` names them CGI-style —
`Http_Exclusive`, `Http_Timeout`, `Http_LockCookie`, `Http_LockDate`,
`Http_LockAge`, `Http_ExtraFlags`, `Http_ActionFlags` — with `_RAW` variants
(`Timeout`, `LockCookie`, `LockDate`, `LockAge`, `ExtraFlags`, `ActionFlags`)
that *look* like the wire spellings. That is inference, not evidence.

The framing itself lives in `webengine4.dll`.

## Mono does not help, and neither does the local prototype

Mono implements `mode="StateServer"` over **.NET Remoting**:
`SessionStateServerHandler.cs` calls `RemotingConfiguration.Configure(null)` and
`Activator.GetObject(typeof(RemoteStateServer), …)`, against a
`RemoteStateServer : MarshalByRefObject`. It talks to Mono's own state server,
not Microsoft's `aspnet_state.exe`, and nothing in it implements the Microsoft
protocol. It is unusable twice over: wrong protocol, and remoting is absent from
.NET 10 and named in [`PROJECT.md`](../../PROJECT.md) as a dependency this port
exists to remove.

Mono's SQL handler is non-interoperable for the same class of reason — its own
`Sessions` table keyed by `(SessionId, ApplicationName)`, not Microsoft's
`ASPState` schema — so it is no shortcut for [SQL mode](session-sql.md) either.

`../Portable.System.Web` carries the same imported client with 32
`UnsafeNativeMethods` references. Same native code, no reimplementation. Its
`using System.Net.Sockets` is inherited from the original.

Mono remains useful only as design evidence for how the provider seam
decomposes, which is what [`PROJECT.md`](../../PROJECT.md) already permits it
for.

## Undecided: where the transport goes

Three shapes, none chosen:

1. **Managed transport inside the imported client.** Replace only the native
   call sites in `OutOfProcStateClientManager.cs`, leaving lock cookies, retry,
   partitioning, and exclusive acquire as Microsoft wrote them — the part where
   behavioral risk actually lives. Narrowest new-code surface; an imported-source
   deviation in a 725-line file, so it needs the port plan's review-boundary
   evidence and a ledger row.
2. **A port-owned provider.** Leave the imported client dead, write a
   `SessionStateStoreProviderBase` that speaks the protocol, and point the mode
   at it. New code under normal authorship rules — but `SessionStateModule`
   constructs `OutOfProcSessionStateStore` directly in its mode switch
   (`SessionStateModule.cs:363`), so an imported deviation happens anyway, and
   this reimplements the locking semantics rather than reusing them.
3. **Managed bodies for the `SessionND*` entry points.** Zero imported diff, but
   P41's contract is that reaching `UnsafeNativeMethods` means refusal — 208
   entry points, no branches. Making some members real turns a categorical
   guarantee into a conditional one.

The choice depends on facts nobody has yet, which is why it is open rather than
recommended.

## Next move: capture the client, not the server

Before any seam is chosen, observe what the native client actually emits. Point
a Framework application on `winbox` at
`stateConnectionString="tcpip=127.0.0.1:42999"` with a small byte-dumping
listener on that port. The native client then shows its literal request bytes.

This answers the question that sizes everything else — **can `HttpClient` speak
this, or does it need raw sockets?** `HttpClient` is clearly preferable if the
server is compliant enough, since framing, pooling, timeouts, and cancellation
come free. Whether it is compliant enough turns on things only observation
settles: whether the status line carries an `HTTP/1.1` prefix or is bare
`200 OK` (bare is unparseable by `SocketsHttpHandler`, a hard blocker), whether
the server tolerates the `Host` header and connection management `HttpClient`
always applies, and whether the body is framed with a standards-compliant
`Content-Length`.

The capture needs **no** `aspnet_state` service, which is why it comes first.

## The service question

`aspnet_state.exe` is present on `winbox` at
`C:/Windows/Microsoft.NET/Framework64/v4.0.30319/aspnet_state.exe`, but
`Get-Service aspnet_state` finds nothing — it is not registered. Registering it
is a durable change to the validation host and has not been approved. Only the
end-to-end tests need it; the capture spike does not.

Note also that the service is Windows-only, so this mode requires a Windows
machine somewhere in a deployment even when the client is portable — the same
shape as depending on SQL Server, and a boundary this story must state
explicitly when it lands.

## Done when

- The capture spike answers the `HttpClient`-versus-sockets question with
  observed bytes.
- A seam is chosen with a ledger row and the review-boundary evidence the port
  plan requires.
- An application configured `mode="StateServer"` reaches a real
  `aspnet_state.exe`, and the preflight refusal in
  [session state](session-state.md) is replaced by a supported-mode row.
