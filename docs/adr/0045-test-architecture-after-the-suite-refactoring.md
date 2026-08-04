---
status: accepted
---

# Test architecture after the 2026-08 suite refactoring

The test suite grew bootstrap-era structure faster than it shed it: four
copies of one host skeleton, gates that shelled out three process levels deep,
goldens unreviewable in diffs, and rules spread over a chain of ADRs. The
2026-08 refactoring ([plan](../follow-ups/test-suite-refactoring.md)) replaced
that structure. This ADR records the operative decisions; the
[writing-tests guide](../writing-tests.md) is the day-to-day form.

## Decision

- **Kestrel scenario tests are the primary test path.** They exercise the
  production adapter end to end and are where coverage grows first.
- **The Framework oracle is a frozen reference instrument.** The committed
  golden remains a standing regression gate
  ([ADR 0031](0031-require-the-first-slice-parity-gate.md)), but new golden
  sessions are exceptional. New Framework evidence prefers captured fixtures
  (the `Framework.postback` pattern) or ad-hoc oracle readings, under
  [ADR 0044](0044-gate-differentials-by-evidence-not-by-slice.md)'s evidence
  rule.
- **Sharing is the default.** A new scenario joins an existing fixture's
  shared host. A new fixture or process requires a structural reason:
  conflicting configuration, a cold/activation claim, on-disk mutation, or
  process death. The bar is recorded with the fixtures
  (`tests/Rehost.WebForms.ScenarioHost/fixtures/README.md`).
- **Process trees from tests are one level deep.** Each child is one
  application activation — the one-application-per-process constraint is the
  only reason a child exists. Orchestration and comparison run in the test
  process.
- **Assertions are response-first.** The typed `HttpResponse` is the primary
  contract; server-side facts go through the typed journal (and, as probes
  migrate, a witness endpoint); raw trace strings never appear in tests.
- **Protocol strings have one typed definition site** (labels, CLI options,
  environment variables, journal grammar). Pinned expected values — golden
  hashes, rendered markup, error text — stay literal in tests: the literal is
  the assertion.
- **Staleness is impossible by design.** Every project is a member of
  `Rehost.WebForms.slnx`; build once, `dotnet test --no-build` runs current
  binaries in either configuration; harnesses derive the configuration from
  their own output path. The net481 oracle compiles everywhere via reference
  assemblies and runs only on Windows.

## Consequences

Adding a scenario assertion usually means a probe or page in an existing
fixture and an act→assert test over the shared host — no new process, fixture,
or golden. The Windows oracle box is needed only for golden regeneration and
ad-hoc Framework readings, not for routine test work. Old-style tests migrate
opportunistically; the remainder is tracked in the plan's checklist.
