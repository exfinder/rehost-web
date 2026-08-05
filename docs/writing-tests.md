# Writing tests

The single entry point for test authors. The architecture behind these rules
is [ADR 0045](adr/0045-test-architecture-after-the-suite-refactoring.md).

## Which kind of test

Walk down; stop at the first rung that fits.

1. **Plain unit test** — the default. Lives in the folder mirroring the source
   it covers (`tests/Rehost.WebForms.Runtime.Tests/<mirrored path>/`), uses a
   disposable temp directory if it touches disk, spawns nothing.
2. **Scenario over the shared host** — for claims about a running application
   over Kestrel. Join an existing fixture's shared host (`Fixtures.*` in
   Hosting.Tests); add a probe or page to the fixture and assert by response.
   A new fixture or a new process needs a structural reason — conflicting
   configuration, a cold/activation claim, on-disk mutation, process death —
   recorded in
   [the fixtures README](../tests/Rehost.WebForms.ScenarioHost/fixtures/README.md).
3. **Differential** — only where only Framework can decide the outcome
   ([ADR 0044](adr/0044-gate-differentials-by-evidence-not-by-slice.md)):
   the port replaced native/host-owned code, or must match a format it cannot
   derive from its own inputs. Prefer a captured fixture (the
   `Framework.postback` pattern) or an ad-hoc oracle reading over a new golden
   session; see [tests/parity/README.md](../tests/parity/README.md).

## Scenario shape

Act in the test, against a passively-serving host; assert on the typed
response:

```csharp
var response = await scenario.Client.GetAsync("/Default.aspx");
response.StatusCode.ShouldBe(200);
response.Text.ShouldContain("...");
```

- `LiveScenario` starts the host for a `ScenarioFixture`; `ScenarioClient` is
  the deterministic client (no redirects, no cookies, exact HTTP/1.1).
- Assertion hierarchy: the typed response first; server-side facts through the
  typed journal (`scenario.Journal`, a `ScenarioJournalReader`) or the
  fixture's witness endpoint (`scenario.Witness`). Live facts go through the
  witness; nothing new may poll the file journal — a polled shared file once
  lost an abort marker to Windows sharing semantics (see
  follow-ups/client-reset-detection-latency.md). Raw trace strings never
  appear in tests —
  string negatives over a trace can pass vacuously.
- Tests spawn at most one level of child processes, and only because a child
  is one application activation. Orchestration stays in the test process.
- Test-side helpers run in an unactivated process, so a System.Web API that
  reads configuration throws there — `HttpUtility`'s encoder is one. Use the
  BCL (`WebUtility`, `Uri`) in anything the test process itself runs.

## Rules that keep tests honest

- A test that a stub implementation would also satisfy is not covering the
  behavior. Know what the test looks like when it fails, and prefer inputs
  that fail when the implementation degrades (AGENTS.md).
- Protocol strings (labels, CLI options, environment variables, journal
  grammar) have exactly one typed definition site. Pinned expected values —
  golden hashes, rendered markup, error text — stay literal in the test: the
  literal is the assertion.
- One type per file; xUnit `[CollectionDefinition]` markers may co-locate with
  their fixture.
- Both supported platforms must pass before work is reported done; the loop is
  in [windows-validation-host.md](windows-validation-host.md). One solution
  build, then `dotnet test` per project or solution-wide — both are supported.

## Why not WebApplicationFactory

`Mvc.Testing` hosts the application inside the test process. This runtime
permanently mutates process-global state on activation (one application per
process is a hard constraint), and `TestServer` has no transport, which the
body/abort scenarios assert on. The shared-host fixtures play the
`WebApplicationFactory` role with child processes instead. Plain
`Microsoft.AspNetCore.TestHost` remains an option only for hypothetical
middleware with no runtime activation.

## Old-style tests

Scenario tests written before the act→assert style migrate opportunistically;
the remainder is tracked in the
[refactoring plan's checklist](follow-ups/test-suite-refactoring.md).
