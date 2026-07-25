# ASP.NET Core host adapter

Status: open. Priority: highest. Contract:
[managed runtime port plan, slice 1D](../core-runtime-port-plan.md#1d-add-the-kestrel-adapter).

## Problem

Kestrel must enter System.Web through public
`HttpRuntime.ProcessRequest(HttpWorkerRequest)` without importing IIS hosting
assumptions or owning application lifecycle.

## Required decisions

- Bodyless request mapping with empty `PathInfo`.
- Minimal server-variable set and behavior for IIS-only variables.
- Route/pass-through rule for the configured precompiled-handler surface.
- Exactly-once completion and pre-pipeline escape handling.
- Disconnect observation without abandoned pipeline ownership.
- Explicit failures for deferred worker-request methods.

Do not inherit POC mappings without checking System.Web consumers and ASP.NET
Core semantics. Do not use `Task.Run` or enable Kestrel synchronous I/O.

## Verification

Unit-test each first-slice mapping, including encoded paths, repeated headers,
missing connection data, virtual-root hosting, and unsupported members. Test
transport integration separately from core differential probes.

## Done when

The adapter exposes the accepted first-slice transport envelope without
IIS/native state, unsafe path construction, synchronous Kestrel I/O, or silent
placeholder values.
