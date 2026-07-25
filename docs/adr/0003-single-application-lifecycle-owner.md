---
status: accepted
---

# Use one application lifecycle owner

One process-wide application runtime owns configuration validation, legacy
state binding, managed hosting initialization, request admission, failure, and
shutdown. Application bootstrap is an internal startup phase, not an
independently ready application state.

This prevents hosts and requests from observing the partial static state that
the Framework safely contained through controlled child-AppDomain activation.
