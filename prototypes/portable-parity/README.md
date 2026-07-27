# Portable core parity diagnostic

Infrastructure-only prototype. It targets `net10.0`; it is not referenced by
the production solution and does not change runtime behavior.

The portable and .NET Framework adapters share:

- one `netstandard2.0` request/observation contract;
- the recording `HttpWorkerRequest` and request runner source;
- module and precompiled-handler probe source;
- the same application `web.config`;
- the empty normalization manifest and generated Framework golden trace.

The host calls the existing public sequence:

```text
WebFormsApplication.Initialize
→ ApplicationManager.CreateObject
→ HttpRuntime.ProcessRequest(HttpWorkerRequest)
```

`CoreParity.Probes.dll` is staged only in `fixture/app/bin`. The host neither
references nor preloads it.

## Build

From this directory:

```shell
dotnet build PortableParity.slnx -c Release
```

## Run

```shell
dotnet src/PortableParity.Host/bin/Release/net10.0/PortableParity.Host.dll run
```

Success emits a deterministic portable trace to standard output. Failure emits
a deterministic JSON diagnostic to standard error with the failed phase and
the original exception chain.

## Current reachable state

Activation completes and request ownership transfers. `run` exits zero and
emits a `PipelineTrace` whose events include `runner.process-request.enter`.
There is no longer an activation-phase `FailureDiagnostic`.

`HttpRuntime.ProcessRequest` currently returns without dispatching the
configured module or precompiled handler, so the observed trace is:

```text
runner.process-request.enter
worker.end-of-request
runner.process-request.return
```

against an empty `200`. The next blocker is therefore pipeline dispatch —
module collection/initialization and handler selection — not activation.

`verify` still exits nonzero, because module, handler, and response parity
against the Framework golden trace are not implemented. Do not add it as an
always-passing CI test until that slice lands.

Every platform dependency reached during activation is classified as P01–P25 in
the [portability ledger](../../docs/portability-ledger.md). Six carry recorded
deviations rather than parity: process-scoped auto-generated machine keys (P07);
a codegen directory with no generation segment (P08); config map path selection narrowed to the hosting map for all paths (P09); cache
size sampling permanently inactive (P12); a zero-seeded memory pressure history
until the first collection (P21); and `IsHidden` classifying a different file set
per OS (P23).

## Verify

```shell
dotnet src/PortableParity.Host/bin/Release/net10.0/PortableParity.Host.dll verify
```

Verification requires exact scenario, schema, ordered events, response,
escaped-exception shape, and completion counts against the committed Framework
`cold-sync.json`. Adapter-specific provenance is excluded. The checked-in
normalization manifest must exist and remain empty.

Optional explicit inputs:

```shell
dotnet src/PortableParity.Host/bin/Release/net10.0/PortableParity.Host.dll verify \
  --expected ../framework-oracle/artifacts/golden/cold-sync.json \
  --normalization ../framework-oracle/metadata/normalization.json
```

The command intentionally exits nonzero at the current blocker. Do not add it
as an always-failing normal CI test until the reachable runtime slice advances.
