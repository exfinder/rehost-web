---
status: accepted
---

# Use a five-state application lifecycle

The application owner has five states:

`Registered → Activating → Accepting → StopRequested → Stopped`

Concurrent first requests await the same activation transition. An activation
escape stores its terminal cause and enters `StopRequested`. A request already
owned by System.Web may complete, while new requests are rejected.

There is no separate catch-all `Faulted` state. The later lifecycle slice owns
draining, disposal, and the final transition to `Stopped`.
