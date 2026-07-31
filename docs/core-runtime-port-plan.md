# Managed Web Forms runtime port plan

Status: accepted roadmap; slices 0–3 implemented, slice 4 current.

This document owns slice order and cross-slice gates. See
[`PROJECT.md`](../PROJECT.md) for project policy,
[the runtime model](classic-managed-runtime-model.md) for sequencing and
lifetimes, [bootstrap](application-bootstrap-and-configuration.md) for the
application contract, [ADRs](adr/) for rationale, and the
[portability ledger](portability-ledger.md) for reached evidence.

## Vertical slices

| Slice | State | Scope owner | Principal gate |
| ---: | --- | --- | --- |
| 0 | Passing | [.NET Framework oracle](follow-ups/framework-differential-harness.md), root-config provenance, ledger | reproducible traces; narrow normalization; every mismatch resolved |
| 1 | Complete | [First runnable request](follow-ups/first-runnable-request.md) | bodyless configured precompiled handler matches core and adapter probes |
| 2 | Complete | [Runtime codegen/loading](follow-ups/runtime-codegen-and-loading.md) | pre-app-start, `App_Code`, `Global.asax`, and `Application_Start` ordering passes port-local gates by [ADR 0043](adr/0043-gate-the-compilation-substrate-locally.md) |
| 3 | Complete | [Dynamic ASPX integration](follow-ups/dynamic-aspx-integration.md) | deterministic `.aspx` GET lifecycle and output passes port-local gates by [ADR 0044](adr/0044-gate-differentials-by-evidence-not-by-slice.md) |
| 4 | Current | [Deferred request surfaces](follow-ups/deferred-request-surfaces.md): body bridge, forms, postback, view state, uploads | sync/async entity-body parity without assumed Kestrel synchronous I/O |
| 5 | Queued | [Deferred request surfaces](follow-ups/deferred-request-surfaces.md): session, cache, authentication, resources, routing | feature-specific configuration and request parity |
| 6 | Queued | [Process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md) and broader transport | graceful lifecycle plus transport-specific gates |

Do not begin a slice until its predecessor's principal gate passes.

## Review boundaries

Imported Reference Source edits require:

- a ledger row naming the blocked leaf;
- evidence that config/inactive behavior cannot satisfy the postcondition;
- retained Framework code under `NETFRAMEWORK` where useful;
- a focused unit test and relevant differential probe.

Host integration edits must not:

- initialize `HostingEnvironment` directly from middleware;
- select handlers;
- expose `ApplicationManager` publicly;
- use ASP.NET Core DI to replace System.Web object lifetimes implicitly;
- stop awaiting because a client disconnected.
