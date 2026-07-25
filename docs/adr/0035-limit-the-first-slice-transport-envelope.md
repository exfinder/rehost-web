---
status: accepted
---

# Limit the first-slice transport envelope

The first Kestrel adapter slice supports bodyless requests, empty `PathInfo`,
standard method/path/query/protocol/scheme/address/header metadata, response
status and headers, memory response fragments, final flush, and exactly-once
completion.

Request-body streaming, inferred path info, intermediate streaming flush,
file-send operations, WebSockets, and client certificates are deferred.
Unsupported use fails explicitly at the adapter boundary or the specific worker
request leaf.

Do not copy the sibling POC's eager full-body buffering, path-info filename
heuristic, cancellation-abandoned completion, or claim those behaviors as
general compatibility.
