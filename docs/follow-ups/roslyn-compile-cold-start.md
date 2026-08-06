# Roslyn compile cold start

Status: measured, nothing proposed for landing (2026-08-06). Both remedies are
architectural decisions rather than hotfixes, so this records the evidence and
the options and stops there. Figures are macOS `arm64`, Debug, .NET 10.0.10,
Roslyn 5.6.0, an idle machine; Windows is unmeasured.

## What the cost actually is

Compilation is not expensive per compile. It is expensive once per process.

Repeated compiles in one process, 172 shared-framework references:

| | wall | of which JIT |
| --- | --- | --- |
| first `CSharpCompilation.Create` + `Emit` | 518–594 ms | 447–504 ms |
| second | 9–11 ms | 6 ms |
| third and later | 2–3 ms | ~0 ms |

`System.Runtime.JitInfo` attributes ~85% of the first compile to jitting Roslyn
itself: 8173 methods, 512 KB of IL. The reference set is not the cost —
building a *fresh* `MetadataReference` list over all 172 assemblies and
compiling against it, after JIT is warm, costs 15–25 ms, because Roslyn's
process-wide metadata cache keys on file path and timestamp. Compiling against
5 references first and 172 second puts the 172-reference compile at 43 ms.

`RoslynCSharpCompiler` already holds the reference set in a static `Lazy`, so
nothing is being rebuilt per compile. The per-process floor is JIT.

End to end in the scenario host, `page` fixture with one `/Default.aspx`
request: 1.70 s cold against 0.55 s when the codegen root already holds the
output. Fixtures carrying no `.aspx`, `App_Code`, or `Global.asax` (`body`,
`body-preload`, `body-customerrors`, `legacy-target`, `modern-target`) measure
0.55 s either way — they never invoke Roslyn.

This is why Framework did not feel it. `csc.exe` was NGEN'd, so a fresh
compiler process per batch paid no JIT; `VBCSCompiler` later kept a resident
process so nothing was jitted twice. Both are the same lever: never jit the
compiler again.

## Lever 1 — a warm codegen root

`CodegenDirectory.GenerationSegment` derives the segment from a SHA-256 of the
application's *physical* path, so cached output is only found again when the
application directory path is stable. `LiveScenario`, `PageApplication`, and
`ScenarioApplication` copy each fixture into a fresh
`Directory.CreateTempSubdirectory`, which forfeits the cache by construction.
The parity gate already does the opposite — stable `bin/.../fixture/app` with a
persistent `fixture/temp/root` — and its sessions are warm today.

Census of the 37 child processes a full run spawns:

| Population | n | Effect of a warm root |
| --- | --- | --- |
| PageLiveScenario, PostbackScenario, FarmScenario, TimeoutLiveScenario | 4 | −1.14 s, −0.91 s, −0.89 s, −0.76 s |
| Compilation is the claim, or the test edits the application on disk (codegen substrate/compile-error/recompile/reclaim/shared-segment, page compilation/recompile/deployed-reference, the parity codegen-artifact child) | 14 | must stay cold |
| Already warm by design (`CodegenReuseTests`, `PageReuseTests`) | 2 | none |
| Parity sessions sharing the staged `fixture/temp` | 8 | none |
| Nothing to compile (body ×6, legacy/modern-target ×2, memory-limit ×1) | 9 | none |

Four children, ~3.7 s serial. The suite runs those four in four collections
that already execute concurrently — 22 s wall against 112 s of summed test
time — so the wall-clock return is nearer 1 s. The P3 fixture-sharing pass
took most of what was available here.

## Lever 2 — a prejitted compiler

Crossgen of `Microsoft.CodeAnalysis.dll` and `Microsoft.CodeAnalysis.CSharp.dll`
at the pinned version, dropped into the scenario host's output, verified
against real scenarios (identical status codes and trace shape):

| | cold run, `page` fixture |
| --- | --- |
| Roslyn as shipped (IL) | 1.70, 1.75, 1.69 s |
| Roslyn ReadyToRun | 1.25, 1.27, 1.23 s |

In the isolated probe the same images take the first compile from 518 ms to
202–231 ms, and JIT from 447 ms to 73 ms (2021 methods, 82 KB IL).

Constraints found by doing it:

- The image version must match the referenced version exactly. Images built
  against 4.14 produced a 500 on every request —
  `Could not load file or assembly 'Microsoft.CodeAnalysis.CSharp,
  Version=5.6.0.0'` — with no fallback to IL. The failure is loud, which is
  the right shape, but any crossgen step has to consume the same pinned
  version rather than a literal.
- Crossgen2 is RID-specific: `osx-arm64` and `win-x64` both required. Building
  the two assemblies costs ~6 s wall, ~25 s CPU.
- Every process that runs Roslyn needs the images: the scenario host's output,
  `Rehost.WebForms.Runtime.Tests`'s own output (`RoslynCSharpCompilerTests`
  compiles in-process), and both parity hosts. Four output directories.
- Images are ~3× the size (7 MB → 22 MB, 3 MB → 9 MB), so ~+21 MB per output
  directory.
- The first load of a freshly written image cost 3.47 s once on macOS, then
  1.25 s steadily. In CI that lands on the first child that compiles.

Wiring options, with the obvious one rejected:

- Publishing the scenario host with `PublishReadyToRun` and pointing
  `TestOutputPaths` at the publish output reintroduces exactly the host
  staleness class the suite is built to make impossible. Not viable.
- A post-build target that crossgens the two assemblies in place, keyed on
  package version and RID with `Inputs`/`Outputs` so it stays incremental.
  Deterministic under plain `dotnet build`, no new staleness — but it is build
  infrastructure in four projects plus a RID-specific tool dependency.

Value to the suite: ~0.45 s across the 18 children that compile, so ~8 s
serial and perhaps 1.5–3 s of the 22 s wall. Under the parallel run each child
costs 2–3× its idle figure (`CodegenReclaimTests` 9.6 s for 3 children,
`PageRecompileTests` 8.9 s for 2), and JIT is what they contend on, so the
real return is likely above that estimate.

## The production argument is the stronger one

A rehosted application pays the same ~450 ms of compiler JIT on the first
request that compiles a page, on every process start. `PublishReadyToRun` in a
consumer's publish recovers it with no change here, which makes this at
minimum a deployment recommendation rather than only a test-suite concern.

## What neither lever reaches

The 14 children that must compile cold are unreachable by warming and only
partly helped by prejitting. Removing their cold start entirely means a
resident compiler — `csc /shared` against `VBCSCompiler`, or an equivalent
pipe server — which puts an out-of-process seam into `RoslynCSharpCompiler`
and changes what "cold compile" means for the tests that assert it. That is an
architectural decision to raise before any of it is written, not a performance
tweak, and no evidence here argues for it yet.

## Decisions this needs

1. Whether the four eligible Kestrel fixtures move to stable application paths
   with a shared codegen root, given the return is ~1 s of wall clock.
2. Whether prejitted Roslyn is worth build infrastructure in four projects, or
   whether it stays a documented `PublishReadyToRun` recommendation for
   consumers.
3. If either lands, the same measurement repeated on the Windows host — none
   of the figures above have been reproduced there.

Related: [test-suite refactoring](test-suite-refactoring.md) (the P3 sharing
pass this measures the remainder of),
[runtime codegen and loading](runtime-codegen-and-loading.md).
