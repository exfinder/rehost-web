# Fixture host process reuse

Status: open. Priority: low.

## Intent

One fixture = one host process, reused by every test class on that fixture.
The design intent was always safe reuse — scenarios written to tolerate other
classes' requests interleaving on the same host
([writing-tests.md](../writing-tests.md), rung 2) — but the runner today gives
each class its own process, so the reuse contract is exercised nowhere.

## Current state

- Every `IClassFixture<PageLiveScenario>` class (9 as of 2026-08) spawns its
  own copy of the page-fixture host; other fixtures likewise per class.
- Fixtures are copy-per-host on disk (`LiveScenario` copies into a temp
  directory), so sharing is purely an in-process question: witness journal,
  page statics, output cache.
- xunit collection fixtures share a process but serialize their classes —
  the wrong trade for economy, used only where a shared process is required
  for correctness (postback `machineKey`, body/abort).
- xunit v3 (already the suite's runner) offers assembly fixtures: one instance
  shared across classes that still run in parallel. That is the mechanism for
  one-fixture-one-host without losing parallelism.

## Work

- Audit existing scenarios for reliance on exclusive traffic: untokenized
  witness reads, unkeyed page statics, cache-sensitive pages
  (`compose/Cached.aspx`), anything asserting on host-wide counters.
- Move per-fixture `LiveScenario` instances to assembly fixtures (or an
  equivalent registry keyed by fixture name); keep correctness-isolated
  fixtures (timeout, abort, postback, body) on their dedicated processes.
- Measure: the win is ~one spawn per class saved (1–2 s each, currently paid
  concurrently) plus lower peak process/port count in CI; confirm it is worth
  the shared-tenancy invariant before converting.

## Done when

Classes on the same fixture demonstrably share one host process with class
parallelism intact, every scenario passing under interleaved traffic, and the
fixtures README states which fixtures remain single-tenant and why.
