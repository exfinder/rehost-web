# Test-suite architecture review

Where the test suite's own modules are shallow, where its seams leak, and what
deepening each one would buy. Reviewed at `409c671` over `tests/**` and the
`apps/*/smoke.sh` journeys. Directions here are **proposals**, not decisions;
nothing in this file is in [`backlog.md`](../backlog.md) or
[`ROADMAP.md`](../../ROADMAP.md) yet.

Vocabulary is the deep-module kind: a **module** is anything with an interface
and an implementation; its **interface** is everything a caller must know, not
just the type signature; a module is **deep** when a lot of behavior sits behind
a small interface and **shallow** when the interface is nearly as complex as the
implementation; a **seam** is where the interface lives.

## Result in one paragraph

The harness core is in good shape. `ScenarioHostRegistry`, `ScenarioClient`,
`ParityGateRunner`, and the parity comparison rig are deep modules and need
nothing. The friction is in the modules *around* them: thirteen pass-through
façades over one registry, the on-disk staging recipe written out five times in
two projects, and a witness channel whose three-part protocol is held together
by convention at every call site. Three of the eight candidates below have
defects live in the tree, not just duplication. Nothing here contradicts
[ADR 0005](../adr/0005-evidence-and-test-strategy.md); the shared-host model it
decided is what makes most of these deepenings possible.

## Candidates

| # | Candidate | Strength | Live defect |
| --- | --- | --- | --- |
| 01 | Collapse the `LiveScenario` façade family | Strong | yes |
| 02 | One staged-application module, not five | Strong | no |
| 03 | Fold the witness token into the request | Strong | yes |
| 04 | Make the scenario-host invocation mode-safe | Strong | yes |
| 05 | Split the scenario protocol out of `Parity.Contracts` | Worth exploring | no |
| 06 | Give fixture probe paths one definition site | Worth exploring | no |
| 07 | The app journey lives in bash | Worth exploring | no |
| 08 | Case-sensitive skip, solved three ways | Speculative | no |

## 01 — Collapse the `LiveScenario` façade family

**Files.** `tests/Rehost.WebForms.Hosting.Tests/Harness/*LiveScenario.cs` (13
types), `ScenarioHostRegistry.cs`, `ScenarioFixture.cs`, `Fixtures.cs`,
`HostWitness.cs`, `ScopedWitness.cs`.

**Problem.** Thirteen modules, 189 lines, no implementation — every member
forwards to `LiveScenario`. xUnit's `IClassFixture<T>` needs a distinct type per
fixture, which is why they exist, but the member set each one exposes is picked
by hand, and the invariant that keeps them honest lives in a comment.

```text
                    which members?          witness kind
  Page              Client Witness Addr Path  scoped
  Postback          Client        Addr        —
  Body              Client        Addr        —
  Abort                    Witness Addr       WHOLE-PROCESS
  Session           Client Witness            WHOLE-PROCESS
  SessionCustom     Client Witness            WHOLE-PROCESS
  AsyncApp          Client Witness            WHOLE-PROCESS, fixture serves none
  FriendlyUrls      Client Witness Addr       WHOLE-PROCESS, fixture serves none
  DefDocDisabled    Client                    —
  WebServer         Client        Addr Path   —
  Sweep             Client Witness            isolated host
  Timeout           Client Witness            isolated host
  CaseSensitive     (exposes LiveScenario)    isolated host
```

**Evidence.**

- `HostWitness.cs` states that only dedicated hosts expose that type and shared
  hosts hand out `ScopedWitness`. Five registry-shared façades hand out
  `HostWitness` anyway; `PageLiveScenario` is the only one that obeys.
- `FriendlyUrlsLiveScenario` and `AsyncAppLiveScenario` expose `Witness`, but
  neither fixture's `web.config` registers `WitnessHandler`. The member 404s at
  runtime and compiles clean.
- Two classes already read the whole-process witness on a shared host:
  `SessionStateOverKestrelTests`, `CustomSessionStoreOverKestrelTests`.
- A fourteenth façade is in the working tree, repeating the same shape.

**Solution.** One generic module over the registry, and move witness scoping
onto `ScenarioFixture` so the interface is derived rather than typed out:

```text
  Fixtures.cs
    Page     = new("page",     Witness.Scoped)
    Session  = new("session",  Witness.Scoped)
    AsyncApp = new("asyncapp", Witness.None)     // cannot expose one
    Timeout  = new("timeout",  Witness.Whole, Isolated)
```

**Wins.** Witness scoping is decided once. A new fixture is a record line, not
a file. Thirteen shallow modules delete. The dead `Witness` members become
unspellable.

## 02 — One staged-application module, not five

**Files.** `Hosting.Tests/Harness/LiveScenario.cs`, `Harness/BatchRun.cs`,
`Runtime.Tests/Compatibility/Compilation/{PageApplication,BatchApplication}.cs`,
`Compatibility/Hosting/{ApplicationConfigurationPublicationTests,ProcessMemoryLimitConfigurationTests}.cs`,
`TestSupport/{TempDirectory,TestFiles,ScenarioHostInvocation}.cs`.

**Problem.** The recipe for staging an application on disk — temp root,
`app/` + `temp/` + `responses/`, trace path, fixture copy, invocation, wait,
read trace, delete — is re-derived at five call sites across two projects, and
each disposes differently.

```text
                 Live      Batch     Page      Batch     2 test
                 Scenario  Run       App       App       classes
  temp root        x         x         x         x         x
  app/temp/resp    x         x         x         x         x
  copy fixture     x         x         x         x         x
  invocation       x         x         x         x         x
  wait + trace     x         x         x         x         x
  dispose        swallow   swallow   swallow   swallow   finally
```

**Evidence.**

- 32 `Directory.CreateTempSubdirectory` sites across 23 test files.
  `TestSupport.TempDirectory` exists for exactly this and has zero
  `Runtime.Tests` consumers.
- `BatchApplication` and `BatchRun` are the same module either side of a project
  seam: identical indexed `responses/N.body` readers, identical swallowing
  dispose.
- `PageApplication.Run` and `BatchApplication.Run` are the same six lines, down
  to `ExitCode.ShouldBe(0, StandardError)`, and both carry the same
  timestamp-bumping edit helper with the same comment.
- Three cleanup idioms coexist: swallowing `try`/`catch`, bare `try`/`finally`,
  and an `IDisposable` field on the test class.

**Solution.** One `StagedApplication` in `TestSupport`, beside
`ScenarioHostInvocation`, which already owns the CLI half of the same job. The
five consumers keep only what makes them different.

**Wins.** One layout, one dispose. Cleanup stops varying by author. The
interface becomes `Stage(fixture)` rather than a recipe to follow.

## 03 — Fold the witness token into the request

**Files.** `Harness/{WitnessToken,HostWitness,ScopedWitness,ScenarioClient}.cs`
and `{ResponseEnd,Timeout,TimeoutSweep,HeaderAmendment,ServerTransfer}OverKestrelTests.cs`.

**Problem.** Three steps the test must keep in sync, repeated 16 times:

```text
  var token    = WitnessToken.For(this);
  var response = await scenario.Client.GetAsync(path + "?" + WitnessToken.Query(token));
  var stages   = await scenario.Witness.StagesAsync(token);
```

Drop the query and `StagesAsync` returns an empty array. Positive assertions
fail loudly; negative ones pass silently.

**Evidence.** 13 assertions across the five classes pass on an empty stage array
(`stages.ShouldNotContain`, `stages.ShouldAllBe`, `SkippedStages.ShouldAllBe`);
`ResponseEndOverKestrelTests` holds 10 of them. There are zero non-empty guards.
[Writing tests](../writing-tests.md) already bans this shape over the trace file
— "string negatives over a trace can pass vacuously" — but the witness channel
it recommends instead has the same hole.

**Solution.** Move the token inside the request module; return response and
stages together and treat an empty stage array as an error:

```text
  var (response, stages) = await scenario.TracedGetAsync(this, "/End.aspx");
```

**Wins.** Three steps become one. A token mismatch becomes unspellable. Vacuous
negatives fail. Pairs with candidate 01 — both live on the witness seam.

## 04 — Make the scenario-host invocation mode-safe

**Files.** `tests/Rehost.WebForms.ScenarioHost/Program.cs` (526 lines, three
types), `TestSupport/ScenarioHostInvocation.cs`.

**Problem.** `ScenarioHostInvocation` is a shallow adapter: twelve methods over
twelve option strings, with the mode/option contract living nowhere. The builder
composes invocations whose options the host silently drops.

| Option | Honored under | Ignored under |
| --- | --- | --- |
| `--machine-config` | batch | `--serve` (hard-codes the default) |
| `--hold-gate` | batch | `--serve` |
| `--kestrel-max-body` | `--serve` | batch |
| `--http2` | `--serve` | batch |
| `--postback` | `--serve` | batch |

**Evidence.** `ServeAsync` never reads `options.MachineConfigurationPath` at
all. Response-file indexing differs by mode — batch keys by request index,
serve-postback by its own counter — so two index spaces write into one
`--response-dir`. `"codegen-dir:"` is written to the trace and read by nothing.
Every option string is spelled three times: the `case` label, the `Require`
message, and the builder method.

**Solution.** Split the builder by mode (`ServeInvocation`, `BatchInvocation`)
over a shared core, and give `Program.cs` one parse per mode. Two adapters make
the seam real rather than hypothetical: an option the chosen mode cannot honor
has no method to call.

**Wins.** Silently-dropped options become impossible. The mode contract lands in
one type. Response indexing gets one owner.

## 05 — Split the scenario protocol out of `Parity.Contracts`

**Files.** `tests/parity/src/Rehost.WebForms.Parity.Contracts/{WitnessProtocol,TraceEvents,TraceChannel,ProbeHeaders}.cs`,
`Rehost.WebForms.Hosting.Tests.csproj`, `Rehost.WebForms.TestSupport.csproj`.

**Problem.** One assembly holds two disjoint vocabularies, and the scenario half
is reached through a project reference that names the parity harness instead.

```text
  Parity.Contracts (netstandard2.0)
    ├── parity vocabulary    SessionManifest, PipelineEvents,
    │                        IClassicPipelineRunner, ParityGate
    │       ▲ used by Parity.Runner / Probes / AdapterHost
    │
    └── scenario vocabulary  WitnessProtocol, TraceEvents,
                             TraceChannel, ProbeHeaders
            ▲ used by ScenarioHost, ScenarioProbes,
              Hosting.Tests (9 files), Runtime.Tests (11 files)
              — the last two through an undeclared transitive edge
```

**Evidence.** Grepping `tests/parity/` for the four scenario types returns zero
hits outside `Parity.Contracts` itself: parity does not use them. Twenty source
files across the two test projects `using Rehost.WebForms.Parity.Contracts` with
no csproj naming it. `TestSupport`, whose largest type is a 24-line temp
directory, drags in the `net481;net10.0` parity harness to reach one path
helper. Nothing in `docs/`, `CONTEXT.md`, or the ADRs mentions
`Parity.Contracts`; the only justification is a comment in `TraceChannel.cs`.

**Solution.** Move the four scenario protocol files into their own assembly and
reference it by name from the projects that use it.

**Wins.** The dependency edge stops being implicit. The scenario protocol gets a
named home. `TestSupport` sheds the `net481` leg.

## 06 — Give fixture probe paths one definition site

**Files.** `tests/Rehost.WebForms.ScenarioHost/fixtures/*/web.config` (18
files), `Harness/HostWitness.cs`, `Harness/Fixtures.cs`, `ScenarioProbes/*`.

**Problem.** A probe's URL is its interface, and it is spelled independently in
the fixture XML and in every test that calls it.

```text
  TEST CODE                        FIXTURE web.config
  "/body?mode=..."      <-- ? -->  path="body"
  "/cookies?mode=..."   <-- ? -->  path="cookies"
  "/witness"            <-- ? -->  path="witness"   (in 7 files)
  ~60 path literals                nothing ties the two sides together
```

[Writing tests](../writing-tests.md) requires exactly one typed definition site
for protocol strings. The trace and witness *prefixes* obey it; the probe paths
do not.

**Evidence.** `WitnessHandler` is registered in 7 fixture configs;
`RequestBodyHandler` in 3. The config preamble is copy-pasted:
`<compilation targetFramework="4.8" />` in 15 files, `<customErrors mode="Off" />`
in 15, `<authentication mode="None" />` in 14. Three `Runtime.Tests` call sites
bypass `Fixtures.cs` with bare fixture-name literals, because that project
cannot see the record list.

Two staging traps surfaced alongside, both in
`Rehost.WebForms.ScenarioHost.csproj`: newer output wins, so a fixture the
[fixtures README](../../tests/Rehost.WebForms.ScenarioHost/fixtures/README.md)
sanctions mutating in place is never restored from source short of a clean
build; and `PruneStaleScenarioFixtures` scans top-level fixture directories
only, so an emptied nested subdirectory survives in the output tree, where
directory-request scenarios can see it.

**Solution.** One typed definition per probe path, read by the test and by the
generated handler block at staging time. A shared config preamble would close
candidate 01's dead-`Witness` hole from the other side.

**Wins.** Renaming a probe path is one edit. Fixture and test cannot drift.

## 07 — The app journey lives in bash

**Files.** `apps/WebFormsApplication/smoke.sh` (92 lines),
`apps/WebFormsIdentityApplication/smoke.sh` (189),
`eng/app-linux-smoke.sh`, `Hosting.Tests/Harness/PostbackForm.cs`.

**Problem.** The postback parser exists twice, in two languages:

```text
  PostbackForm.cs   Action(html) · Fields(html) · Body(html, overrides)
                    typed, regex, WebUtility decode
        same job
  smoke.sh          field()        tr '<' '\n' | grep | sed
                    submit_name()  grep -o 'name="[^"]*"'
                    postback_target()
```

**Evidence.** Neither script runs under `dotnet test`, and the repository has no
CI configuration at all — both journeys are hand-run, one round per platform.
[Compatibility](../compatibility.md) calls the Identity journey "a manual run
rather than a standing test" while citing it as evidence for a Supported row.

**Solution.** Move the journey assertions to a scenario over the app's own host,
reusing `ScenarioClient` and `PostbackForm`; leave `smoke.sh` as a deployment
probe that answers whether a deployed host is up and serving.

**Wins.** One postback parser, two consumers. A Supported claim gets a standing
gate. About 120 lines of shell parsing delete.

The three-OS CI matrix this would eventually want is already parked in
[the backlog](../backlog.md); moving the claims behind a typed interface does
not depend on it.

## 08 — Case-sensitive skip, solved three ways

**Files.** `TestSupport/CaseSensitiveDirectory.cs`,
`Harness/CaseSensitiveLiveScenario.cs`,
`Runtime.Tests/Compatibility/Util/CanonicalCasePathTests.cs`,
`Runtime.Tests/Util/FileEnumeratorTests.cs`, four `Hosting.Tests` classes.

**Problem.** A nullable `Live` makes every caller decide what "no such
filesystem" means, so three answers coexist: a byte-identical nested `Volume`
class in two `Runtime.Tests` files, an assembly fixture holding
`Lazy<(Volume, LiveScenario?)>`, and a private `RequireLive()` copied into four
`Hosting.Tests` classes. The skip message
`"No case-sensitive filesystem is available on this platform."` appears at six
sites and has no definition site.

**Solution.** One `RequireLive()` on the fixture that skips and returns non-null;
delete the nullable member.

**Wins.** One skip, one message, one site. Nullability leaves the interface.

## Top recommendation

**01.** It is the only candidate with defects live in the tree today, and the
pattern is still growing — a fourteenth façade is in the working tree now.
Deepening it moves witness scoping into a type, which is most of what 03 needs.

If reach matters more than defects, **02** touches five modules across two
projects and carries no correctness risk at all.

## What the review did not find

- The shared-host design is sound. `ScenarioHostRegistry`, `ScenarioClient`, and
  `ParityGateRunner` are deep: small interfaces, real behavior behind them.
- Assertion style is uniform. Shouldly in 121 files; xUnit `Assert` appears only
  as `Assert.SkipWhen`, which Shouldly has no equivalent for.
- The parity rig needs nothing: comparison rules, golden lifecycle, and the
  process-per-session model all hold.
- Test-name casing drifts — four satellite projects use PascalCase against the
  house snake_case — but that is a lint, not an architecture problem.

## Method

Read `tests/**` at `409c671` from a detached worktree, plus the two
`apps/*/smoke.sh` journeys. Counts come from grep and `wc` over that tree and
will age; treat them as the shape of the problem rather than as current
measurements.
