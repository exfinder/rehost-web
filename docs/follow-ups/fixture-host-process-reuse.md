# Fixture host process reuse

Status: resolved. Priority: low.

## Outcome

One fixture = one host process for the `page` fixture, the only fixture with
more than one test class. `ScenarioHostRegistry` (xunit v3 assembly fixture)
owns a lazy host registry; `PageLiveScenario` is a class-fixture façade over
it, so the test classes kept their `IClassFixture` wiring and parallelism.
`PageHostSharingTests` pins the tenancy contract red-first: under per-class
spawning at least one observer fails (the first to assert may see only its
own entry and pass).

Decisions that shaped the mechanism:

- Assembly fixtures are eager (measured: created even when a filtered run
  executes no test that uses them), so the assembly fixture owns only the
  registry; hosts spawn on first `GetOrAdd`.
- A failed spawn is cached (`ExecutionAndPublication`) and fails every class
  on the fixture with the same exception. Deliberate blast-radius trade: a
  host that cannot start is environmental, and one loud message beats eight
  staggered 60-second startup timeouts; retry variants either double-spawn
  (`PublicationOnly`) or add lifecycle machinery.
- Refcounted or linger-based lifetimes were rejected: spawn count becomes
  scheduling-dependent, and the end-of-run kill window either orphans the
  child or needs the run-end owner anyway.
- `TimeoutSweepOverKestrelTests` moved to a dedicated host over the page
  payload (`SweepLiveScenario`): its probe presents every registered request
  as expired, which would spuriously time out any co-tenant's in-flight
  request. Single-class and correctness-isolated fixtures stay per-class
  (tenancy section of the
  [fixtures README](../../tests/Rehost.WebForms.ScenarioHost/fixtures/README.md)).
- Witness tokens are collision-free by construction (`WitnessToken.For`
  derives class + caller method); the per-class `StagesAsync` copies
  centralized into `WitnessReader`.

## Measurements (2026-08)

Per-host tax is dominated by first-request warmup, not spawn, and is a
per-host cost (batch compile makes later pages ~free): macOS ≈ 1s
(0.23s spawn + 0.78s warmup), win-oracle (4 cores) ≈ 5–8s (0.8s spawn +
4–7s warmup). Sharing removes 7 of 8 page hosts; the win concentrates on
core-limited Windows, where Hosting.Tests gates the full-solution round.
