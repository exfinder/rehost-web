# Test-suite refactoring plan

Status: P1–P4 complete (2026-08-04); P5 next. Each phase lands on `main`,
green on both platforms, before the next begins. Windows validation once per
phase.

P4 outcome: 248 → 243 tests (SmtpSectionTests, AssemblyIdentityTests, the
EnterpriseServices shape fact, the WebServices assembly-name fact, and the
MemoryLimits range check deleted; the CodegenDirectory tautological assertion
dropped from its kept fact; the two unsupported-feature messages now assert
independent fragments instead of the implementation's own constant). Five
files moved to mirrored folders with namespaces following; the ledger's
class-name references stay valid. CodegenSubstrateHarness split into its
three types; the Kestrel fixture classes own their files; MemorySamplerTests
joined the serialized memory collection. The planned PortableParityGateTests
parallelization guard is obsolete: the P2 rewrite runs the codegen assertions
in an isolated fixture copy. Both platforms 243/243.

P3 outcome: consolidation was driven by a per-test timing inventory (TRX).
Multipart postbacks share the postback host; the reuse tests take their first
run from the shared substrate/page collection fixtures and pay only for the
second; the codegen-artifact gate runs one session per fixture. Serial test
time fell 87.9s → 66.1s on macOS; Windows Runtime.Tests 61s → 51s; 248/248 on
both platforms. The fixture-folding pass found nothing foldable — every
fixture carries a structural justification, now recorded in
`tests/Rehost.WebForms.ScenarioHost/fixtures/README.md`, which sets the bar
for additions. Two plan items adjusted by agreement: the obsolete
"one host run feeding both facts" gate item became the one-session-per-fixture
trim, and the fold item became the justification table.

P2 outcome: seven commits. One multi-target probes project and one runner
project compile against both runtimes; no `<Compile Include>` links remain.
The harness kit (`Rehost.WebForms.Parity.Harness`, 18 single-type files) owns
the launcher (120 s timeout, tree kill), manifest loading, fixture validation,
CLI, tree-based comparer, diagnostics, repository locator, and gate runner;
the three host programs shrank to thin columns (net ~-500 LOC before the kit's
new capabilities). Gates run each golden session as its own theory case with
one child per session — the process tree from tests is one level deep — and
the codegen-artifact test uses an isolated fixture copy. The golden is 939
indented lines (value-equality proven before reformatting); comparison is
tree-based everywhere; `normalization.json`/`provenance.json` and their
validators are gone. The journal has one definition (`TraceJournal` in
Contracts). Windows round: 248/248 — the abort scenario passed under the new
60 s budget this round; the lost-reset investigation stays open in
[client-reset-detection-latency.md](client-reset-detection-latency.md). The
`ScenarioWorkerRequest` merge was evaluated and deferred to the P5 checklist:
a recording-off mode costs more than the class it would replace.

P1 outcome: acceptance met in both configurations on macOS; on Windows 229/240
pass — the 11 failures are the pre-existing abort-detection issue (bisect-proven
to predate P1; evidence in
[client-reset-detection-latency.md](client-reset-detection-latency.md), now
prioritized standalone work). The P4 abort-budget item was pulled forward and is
done. Deviations from the letter of the plan, by agreement: build props scoped
to `tests/parity/` instead of the repo root; transitive content cut by staging
targets instead of `PrivateAssets` (which does not govern content flow).

## Goal

Simplify the whole test suite so future tests concentrate on what matters:
staleness impossible by design, one dependency graph of plain project
references, at most one level of spawned processes, shared hosts by default,
`HttpResponse` as the primary assertion contract, and no ceremony that a test
author cannot justify.

## Decisions this plan encodes

- Kestrel scenario tests are the declared primary test path. The Framework
  oracle is demoted to a frozen reference instrument: committed goldens stay
  as regression gates, no new golden sessions by default, new Framework
  evidence prefers captured fixtures (the `Framework.postback` pattern) or
  ad-hoc oracle readings. ADR 0044's evidence test applies strictly.
- Sharing is the default at every level: a new Kestrel test joins the default
  fixture's shared host; a new fixture or a new process requires a stated
  structural reason (conflicting config, cold/activation claim, on-disk
  mutation, process death). Same justification bar for new fixtures as for
  new processes.
- Tests spawn at most one level of children; each child is one application
  activation; orchestration and comparison run in the test process.
- Assertion hierarchy: typed `HttpResponse` first; typed journal/witness
  events only for server-side facts; raw trace strings never in tests.
- Future standard for server-side evidence: probes accumulate typed events in
  app memory, exposed via response echo or a witness endpoint. The file
  journal remains only for process-death evidence and cross-process codegen
  scenarios. Existing journal kept for now behind a typed reader.
- Protocol strings (labels, CLI flags, env vars, journal grammar) get exactly
  one typed definition site. Pinned expected values (golden hashes, rendered
  markup, error text) stay literal in tests — they are the assertion.
- One type per file; nested private types only when used solely by the
  enclosing type; xUnit `[CollectionDefinition]` markers may co-locate.
- Comments stay concise: code is the documentation. A comment exists only to
  explain unique or non-standard behavior or an edge case — no multiline
  monologues. Applies to test and harness code the same as production code.
- `Mvc.Testing`/`WebApplicationFactory` is out: it hosts in the test process,
  which one-app-per-process forbids. Plain `TestHost` noted as an option only
  for hypothetical runtime-free middleware.
- Worker-request implementations: one per role — production adapter,
  symmetric differential instrument, minimal core-direct delivery (merged
  into the kit), unit-test fakes. No further consolidation.
- Existing scenario-test bodies migrate to the new style opportunistically;
  the trailing work is tracked in this file's follow-up section, not implied.

## P1 — Solution unification (staleness dies here)

1. Move `prototypes/` content to `tests/parity/`; delete the `prototypes/`
   folder. Rename projects to solution convention:
   - `CoreParity.Contracts` → `Rehost.WebForms.Parity.Contracts`
   - `PortableParity.*` → `Rehost.WebForms.Parity.Portable*`
   - `AdapterParity.*` → `Rehost.WebForms.Parity.Adapter*`
   - `FrameworkOracle.*` → `Rehost.WebForms.Parity.Oracle*`
   - `core-parity/shared/` sources become kit project content (P2 shapes the
     projects; P1 may stage them under `tests/parity/shared/` temporarily).
2. All parity projects join `Rehost.WebForms.slnx`. The net481 oracle joins
   compile-only via `Microsoft.NETFramework.ReferenceAssemblies`; it still
   runs only on the Windows box. Delete `PortableParity.slnx`,
   `AdapterParity.slnx`; keep a Windows-only oracle solution filter if the
   box's ritual needs one.
3. Delete the `Exec` target in `Rehost.WebForms.Hosting.Tests.csproj` that
   rebuilds the adapter prototype in Release; replace with ordinary
   `ReferenceOutputAssembly="false"` build-order edges (each carrying a
   why-comment).
4. Consolidate the per-prototype `Directory.Build.props` /
   `Directory.Packages.props` into the repo root; one package graph.
5. Kill every hardcoded configuration path. `bin/Debug` in
   `PageOverKestrelScenarioRun`, `ApplicationConfigurationPublicationTests`,
   `ProcessMemoryLimitConfigurationTests`; `bin/Release` in both gate tests.
   All derive from `AppContext.BaseDirectory` (the
   `CodegenSubstrateHarness` pattern).
6. csproj hygiene: `DefaultItemExcludes` + single `Content` glob for fixture
   folders, defined once in a `tests/` `Directory.Build.props`; cut
   transitive fixture content flow (`PrivateAssets="all"`); remove the dead
   fixture copy from `Hosting.Tests/bin`.

Acceptance: fresh clone → `dotnet build Rehost.WebForms.slnx` →
`dotnet test … --no-build` runs everything current, in either configuration,
with no manual pre-steps.

## P2 — Parity kit extraction (duplication dies here)

1. New shared kit (project split decided during implementation, roughly
   `Parity.Contracts` + a host kit): repo-root discovery, child-process
   launcher (concurrent stream drain, timeout, process-tree kill),
   manifest loading, fixture validation, CLI options type, journal
   writer/reader, phase diagnostics. Every current copy deleted:
   three parity `Program.cs` blocks, both gate-test harnesses, the
   duplicate `HostJournal`, six repo-root walks, five launchers.
2. Probes multi-target: one `Rehost.WebForms.Parity.Probes` with
   `net481;net10.0`, conditional reference (real `System.Web` vs
   `Rehost.WebForms.Runtime`). Same for the shared runner code. All
   `<Compile Include="../..">` links in the tree disappear; the only
   sanctioned non-standard edges are `ReferenceOutputAssembly="false"`
   build-order edges.
3. Parity orchestration becomes a library. Gate tests call it in-proc:
   sessions enumerated from the manifest as theory data, children spawned
   directly (`run-session`), comparison in the test process with structured
   expected/actual per session. Standalone CLI remains a thin `Main` for the
   Windows oracle box. Process tree from tests: depth 1.
4. Golden: reformat the committed trace locally (parse → pretty-print;
   tree-equality check proves values unchanged), comparer becomes tree-based.
   ADR 0030 gets one clarifying line: mechanical reformatting is not
   authoring. `artifacts/generated/` gets gitignored.
5. Delete dead metadata: `normalization.json` (and both empty-manifest
   validators — the invariant moves to the parity README) and
   `provenance.json` (oracle host literals are the single source).
6. ScenarioHost adopts kit pieces where they overlap (launcher, journal,
   worker request for the non-Kestrel path); `ScenarioWorkerRequest` merges
   into the kit's worker request with recording off.

Acceptance: no duplicated launcher/journal/root-walk code; a parity mismatch
reports as a per-session failing test with structured diff; a hung child
fails the test instead of hanging the run.

## P3 — Process and fixture consolidation (speed)

1. First: timing inventory from MTP per-test durations, both platforms.
   Consolidate by data, top offenders first.
2. Shared default host as a collection fixture spanning test classes; the
   decision ladder (default fixture → existing special fixture → new fixture
   with stated reason) becomes the documented rule. Merge
   `MultipartPostbackOverKestrelTests` into the postback host. Apply
   class/collection fixture sharing to the one-test-per-process codegen
   scenario files where their claims allow.
3. `PortableParityGateTests`: one host run feeding both facts; the
   generated-output assertion stops mutating shared build output (dedicated
   temp root) and gets a parallelization guard.
4. Fixture consolidation pass: grow the default fixture; fold `web.config`-
   only variants where configs do not conflict. Byte-exact golden pages stay
   pristine (witness data never rides an asserted-verbatim response).
5. Re-measure; record before/after in this doc.

Acceptance: measurably fewer child processes; suite wall-clock reduced on
the 4-vCPU Windows host; no test shares a process with a claim that forbids
sharing (each exception carries its why-comment, as today).

## P4 — Cleanups

1. Delete zero-value tests (approved list; rewrite-if-defended noted inline):
   - `SmtpSectionTests` — property get/set round-trip + Framework constant
     defaults; a stub satisfies it. Delete file.
   - `AssemblyIdentityTests` — types-live-in-assembly; build-graph
     restatement. Delete file.
   - `EnterpriseServicesContractTests` — enum member/value/accessibility
     restatements. Delete those facts; keep the
     `PlatformNotSupportedException` behavior test.
   - `WebServicesSectionTests.Compatibility_Types_Use_Project_Assembly_Name`
     — same shape. Delete fact; project stays (may evolve).
   - `CodegenDirectoryTests` `f(x)==f(x)` stability fact — tautology; the
     golden-hash fact beside it carries the signal. Delete fact.
   - `MemoryLimitsTests` `ShouldBeInRange(1,100)` — any plausible value
     passes. Delete fact.
   - `RoslynCSharpCompilerTests` message `ShouldBe(UnsupportedMessage)` —
     compares the implementation constant to itself. Rewrite: assert the
     throw plus an independent literal fragment.
   - `XsdBuildProviderTests` — retyped full message literal. Rewrite to
     assert rejection + stable fragment ("not supported").
2. ~~Abort flake~~ — done in P1 (60 s deadline, latency recorded never
   asserted). The deeper lost-reset-under-concurrency finding lives in
   [client-reset-detection-latency.md](client-reset-detection-latency.md).
3. Move misplaced test files to mirrored folders
   (`Compatibility/Util/CounterTests` → `Util/`, root `FileUtilTests` →
   `Util/`, `AutoGeneratedMachineKeyReportTests` → `Compatibility/Hosting/`,
   `UI/ClientStateIdentifierTests` → `Util/`, `Compatibility/Caching/` fact
   → `Compatibility/Hosting/`). Grandfather clause narrows to genuinely
   pre-convention root files.
4. One-type-per-file pass over touched harness code
   (`CodegenSubstrateHarness` split; fixture classes to own files).
5. Timing-sensitive unit tests get isolation where missing
   (`MemorySamplerTests` collection; `PortableParityGateTests` collection).

## P5 — New-test standards live

1. `ScenarioClient`: thin deterministic `HttpClient` wrapper (no redirects,
   no cookies by default, pinned HTTP version, explicit handler, loud
   timeouts) + raw-socket helper for aborts. Built on `PostbackFormClient`.
2. New scenario tests: act in the test (`ScenarioClient` against the shared
   host), assert response-first; server-side facts via typed journal reader
   now, witness endpoint as probes are added. Typed fixture descriptors
   replace fixture-name strings.
3. Trailing migration tracked here:
   - [ ] `RequestBodyOverKestrelTests` shared-fixture facts → act/assert
   - [ ] `PostbackOverKestrelTests` probes → act/assert
   - [ ] `PageOverKestrelTests` → act/assert
   - [ ] `MixedFarmOverKestrelTests` → act/assert
   - [ ] journal file → in-memory witness (journal stays for process-death
         and cross-process codegen evidence)
   - [ ] `ScenarioWorkerRequest` → kit worker request (deferred from P2:
         `RecordingWorkerRequest` records `worker.*` events in every
         override; a recording-off mode costs more than the 101-line class
         it would replace)
   - [ ] ScenarioHost client half deleted as probes migrate

## P6 — Docs and acceptance

1. New ADR: test architecture (shared-host default, response-first contract,
   one-level process rule, oracle demotion, capture-over-golden policy).
2. Supersession front-matter on ADRs 0006, 0040, 0043 (and 0028's IIS-probe
   clause marked dead). PROJECT.md differential wording aligned to ADR 0044.
3. One concise "writing tests" guide (decision ladders, assertion hierarchy,
   magic-string rule, Mvc.Testing answer for future consumers); AGENTS.md
   points to it. Stale parity READMEs collapse into one `tests/parity/`
   README.
4. Solution-wide `dotnet test` acceptance: N consecutive clean runs on the
   Windows host; if flakes persist after P3, cap cross-project parallelism
   via test configuration until they stop.

## Deprioritized (explicitly out)

Normalization engine; oracle CI automation; new golden sessions; IIS
integrated-mode probes; `request-body-concurrency` measurements (own
follow-up); container memory validation (own follow-up).
