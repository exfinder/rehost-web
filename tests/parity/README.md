# Parity rigs

Differential gates for the port. `sessions.json` declares the sessions; the
committed golden under `artifacts/golden/` is what the suite verifies against.
Shared machinery lives in `src/Rehost.Web.Parity.Harness`; probes and
runners are single projects compiled for both runtimes (`net481;net10.0`).

## Columns

| Column | Runtime | Entry | Role |
| --- | --- | --- | --- |
| oracle (`OracleHost`, net481) | real .NET Framework 4.8.1 | `HttpRuntime.ProcessRequest` via `ApplicationManager` | generates the golden; runs only on Windows |
| portable (`PortableHost`) | the port | same managed entry, recording worker request | strict comparison against the golden |
| adapter (`AdapterHost`) | the port over Kestrel | `AddRehostWeb`/`UseRehostWeb`, real sockets | transport-aware comparison |

A session is one process, one fixture, and ordered steps of concurrent
requests; process-per-session is what makes "cold" mean cold. Current
inventory (from `sessions.json`): `cold-then-warm` (6 requests),
`concurrent-cold` (2), `request-bodies` (6) on the `app` fixture; `errors` (3)
on `app-errors`.

## Comparison

Rules live in `TraceComparer`, not in a normalization layer — none exists and
none may be added; a fixture that would need one is a design problem in the
fixture. The strict mode compares everything including header order. The
adapter mode treats headers as a bag, excludes transport-owned `Date`/`Server`,
and skips `worker.*`/`runner.*` events the adapter column cannot observe.

## Gates

`PortableParityGateTests` and `AdapterParityGateTests` enumerate the manifest
as theory cases, spawn one `run-session` child per session through the
harness, and compare in the test process. The hosts' CLIs (`run`, `verify`,
oracle `generate`) are thin wrappers over the same library for manual use and
the Windows box.

## Golden lifecycle

Regeneration is exceptional ([evidence strategy](../../docs/adr/0005-evidence-and-test-strategy.md)):
new Framework evidence prefers captured fixtures or ad-hoc oracle readings.
When a regeneration is warranted, on the Windows box
([eng/win-oracle.md](../../eng/win-oracle.md)):

```text
Rehost.Web.Parity.OracleHost.exe generate --output artifacts\generated\sessions.json
```

Review the indented diff against `artifacts/golden/sessions.json`, promote by
copy, and verify with the oracle's `verify --expected` plus both local gates.
The golden is generated evidence — values are never hand-authored; mechanical
reformatting is not authoring ([evidence strategy](../../docs/adr/0005-evidence-and-test-strategy.md)).
