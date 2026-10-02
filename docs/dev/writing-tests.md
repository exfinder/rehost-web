# Writing tests

The single entry point for test authors. The architecture behind these rules
is [the evidence and test ADR](adr/0005-evidence-and-test-strategy.md).

## Which kind of test

Walk down; stop at the first rung that fits.

0. **Reuse existing coverage.** Untouched imported behavior over substrate already
   exercised on every supported platform may rely on that coverage and the current
   compatibility boundary. First reach of registry, native interop, filesystem,
   crypto or culture behavior requires a focused test on the triggering platform.
   An additional test must guard a meaningful seam; keep evidence in tests rather
   than separate reading tables or inventories.
1. **Plain unit test** — the default. Lives in the folder mirroring the source
   it covers (`tests/Rehost.Web.Tests/<mirrored path>/`), uses a
   disposable temp directory if it touches disk, spawns nothing.
2. **Scenario over the shared host** — for claims about a running application
   over Kestrel. Join an existing fixture's shared host (`Fixtures.*` in
   AspNetCore.Tests); add a probe or page to the fixture and assert by response.
   The API enforces this: `ScenarioHostRegistry.GetOrAdd(fixture, role)` is the
   only door to a shared host, and a private process exists only through
   `LiveScenario.StartIsolated`, which demands a structural `IsolationReason`
   at the call site — the constructor is private, so a duplicate host with the
   same configuration cannot be expressed. Structural reasons are conflicting
   configuration, a cold/activation claim, on-disk mutation, process death,
   and a whole-host negative assertion (a claim that the host *never* did
   something, meaningful only on a pristine process); a new fixture app is
   additionally recorded in
   [the fixtures README](../../tests/Rehost.Web.ScenarioHost/fixtures/README.md).
   The host is shared for real: classes on the `page` fixture reach one
   process through the registry, and `PageHostSharingTests` pins it.
   Probes default to precompiled `ScenarioProbes` handlers registered by
   assembly-qualified type; a fixture `.aspx` exists only when the claim
   exercises page machinery, since every page costs the host a compilation
   episode at startup.
   Write every scenario to stay correct with other classes' requests
   interleaved on the same host — stage reads through
   `scenario.TracedGetAsync(this, …)`, whose class-plus-method token keeps
   streams collision-free, in-application state (page statics, cache
   entries) keyed per request, and no assertion that the host saw only this
   class's traffic. A scenario that cannot meet this has a structural reason
   for isolation; record it with the fixture.
3. **Differential** — only where only Framework can decide the outcome
   ([evidence strategy](adr/0005-evidence-and-test-strategy.md)):
   the port replaced native/host-owned code, or must match a format it cannot
   derive from its own inputs. Prefer a captured fixture (the
   `Framework.postback` pattern) or an ad-hoc oracle reading over a new golden
   session; see [tests/parity/README.md](../../tests/parity/README.md).

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
- Evidence recording must never be able to fail the request being observed:
  a probe that throws from its own recording turns the evidence channel into
  the failure.
- Assertion hierarchy: the typed response first; server-side facts through a
  traced request — `scenario.TracedGetAsync(this, …)` returns the response
  together with its stage stream and throws on an empty one, so a stage
  negative cannot pass vacuously — or, on markers whose base class grants the
  whole-process witness, `scenario.Witness`. Nothing may poll the trace
  file for assertions; Windows sharing can drop a marker observed through polling. Raw trace strings never appear in tests —
  string negatives over a trace can pass vacuously.
- Tests spawn at most one level of child processes, and only because a child
  is one application activation. Orchestration stays in the test process.
- Test-side helpers run in an unactivated process, so a System.Web API that
  reads configuration throws there — `HttpUtility`'s encoder is one. Use the
  BCL (`WebUtility`, `Uri`) in anything the test process itself runs.

## Rules that keep tests honest

- A test that a stub implementation would also satisfy is not covering the
  behavior. Know what the test looks like when it fails, and prefer inputs
  that fail when the implementation degrades.
- A cache can answer for a removed seam. Arrange a miss first (for case folding,
  request the wrongly-cased path before warming compilation), then mutate the
  implementation to verify the test fails.
- Shouldly's string `ShouldContain`, `ShouldStartWith` and `ShouldEndWith`
  compare case-insensitively unless the call passes `Case.Sensitive`;
  `ShouldBe` and the collection overloads do not. Pass `Case.Sensitive`
  wherever casing carries the claim: markup and control ids, wire header
  text, `Boolean.ToString()`, encoded values, paths on a case-sensitive
  volume. Where the whole value is known, prefer
  `ShouldBe`, which is exact and also catches extra content. The negative
  form is the other way round: `ShouldNotContain` is stricter while it stays
  insensitive, so leave it alone.
- Protocol strings (labels, CLI options, environment variables, trace
  grammar) have exactly one typed definition site. Pinned expected values —
  golden hashes, rendered markup, error text — stay literal in the test: the
  literal is the assertion.
- One type per file; xUnit `[CollectionDefinition]` markers may co-locate with
  their fixture.
- Windows x64, Linux, and macOS arm64 must pass before work is reported
  done; the loop is in [windows-validation-host.md](windows-validation-host.md).
  One solution
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
the remaining cleanup is listed in [the backlog](backlog.md#parked).
