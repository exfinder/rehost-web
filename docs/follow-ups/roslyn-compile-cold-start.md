# Roslyn compile cold start

Status: prejitted Roslyn landed for in-repo hosts (2026-08-06). Figures are
macOS `arm64`, Debug, .NET 10.0.10, Roslyn 5.6.0, an idle machine, except where
marked Windows (EC2 `t3a.xlarge`, 4 vCPU).

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
Roslyn's process-wide metadata cache keys on file path and timestamp, and
`RoslynCSharpCompiler` already holds the reference set in a static `Lazy`. The
per-process floor is JIT.

This is why Framework did not feel it. `csc.exe` was NGEN'd, so a fresh
compiler process per batch paid no JIT; `VBCSCompiler` later kept a resident
process so nothing was jitted twice. Both are the same lever: never jit the
compiler again.

## What landed

`eng/RoslynReadyToRun.targets`, imported by one line in each project whose
output runs Roslyn (the scenario host, `Rehost.WebForms.Runtime.Tests`, both
parity hosts, the sample app). `Rehost.WebForms.Runtime` is the sole producer:
it crossgens the two Roslyn assemblies once per Roslyn version, runtime
version, and RID into `artifacts/roslyn-r2r/` (incremental via
`Inputs`/`Outputs`; a single producer keeps parallel solution builds off a
shared write). Every other importer substitutes the images into
`RuntimeCopyLocalItems` — the single source that build copy, publish, and
deps.json all derive from — so output directories are ReadyToRun by
construction under plain `dotnet build`, and reverting the import restores IL
on the next build. Mechanics live in the targets file.

Measured, steady state (`Rehost.WebForms.Runtime.Tests`, `--no-build`):

| | before | after |
| --- | --- | --- |
| macOS suite wall | 18–20 s | ~14 s |
| Windows 4 vCPU suite wall | 48.7–55.4 s | 31.7–39.6 s |
| isolated first compile | 518 ms wall / 447 ms JIT | 192 ms / 67 ms |

The suite win exceeds the serial estimate because JIT was what the parallel
test collections contended on. Costs: ~31 MB per output directory, ~6 s of
crossgen once per machine per version key, ~+0.4 s on a solution-wide
incremental build.

Two findings any future crossgen work should keep:

- crossgen2 without `-O` emits minopt images that leave most of the JIT cost
  in place (269 ms residual against 67 ms).
- crossgen2 silently skips every method that touches an unresolvable
  reference; each Roslyn assembly must be compiled with the other passed as
  `-r`. Coverage loss is invisible in build output, which is why
  `RoslynReadyToRunImageTests` asserts the test-output assemblies carry a
  ReadyToRun header — every failure mode of this infrastructure degrades
  silently to IL, never to incorrect output.

## Contract boundary: package consumers

`eng/RoslynReadyToRun.targets` is repository infrastructure and deliberately
not shipped in the NuGet package. An external consumer prejits by publishing
with `PublishReadyToRun`, which covers Roslyn, this runtime, and their own
assemblies with one flag. Shipping our targets would drag a RID-specific
crossgen2 dependency and a writable shared image cache into arbitrary consumer
builds for no additional benefit. Since process replacement is the restart
model, every process start pays the ~450 ms compiler JIT without it — a
deployment recommendation worth surfacing in consumer-facing docs.

## Not pursued

- A warm codegen root for the four eligible Kestrel fixtures (stable
  application paths so `CodegenDirectory.GenerationSegment` re-finds cached
  output). Its ~1 s projected return was estimated against a JIT-heavy suite;
  with prejit landed the remainder does not justify restructuring fixture
  staging.
- A resident compiler (`VBCSCompiler` equivalent) is the only lever that
  reaches the 14 children that must compile cold. It would put an
  out-of-process seam into `RoslynCSharpCompiler` and change what "cold
  compile" means for the tests that assert it — an architectural decision to
  raise before any of it is written, and no evidence argues for it yet.

Related: [test-suite refactoring](test-suite-refactoring.md),
[runtime codegen and loading](runtime-codegen-and-loading.md).
