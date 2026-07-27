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

The full classic pipeline runs. `verify` exits zero: the portable trace matches
the Framework `cold-sync` golden exactly — nineteen ordered events, `201 Oracle
Created`, five headers in order, `oracle-ok`, one final flush — against an empty
normalization manifest.

The configured `ProbeModule` and `SyncProbeHandler` resolve from
`fixture/app/bin` through classic configuration, with no host reference and no
preload. `DefaultAuthentication` appears after the cleared collection because
`HttpModulesSection.CreateModules` appends it, not because anything registers it.

This completes the first of the eight scenarios in
[the first-slice parity gate](../../docs/adr/0031-require-the-first-slice-parity-gate.md).
Concurrent cold, warm pooled, delayed asynchronous, `CompleteRequest`, module and
handler exceptions, resolution failure, and terminal shutdown remain.

`PortableParityGateTests` in the main solution runs `verify` as a child process,
because a parity run permanently mutates process-global state — the default
`AssemblyLoadContext` resolver, the `HttpRuntime` singleton, and the activated
application — and cannot share a process with other tests.

Every platform dependency reached so far is classified as P01–P32 in the
[portability ledger](../../docs/portability-ledger.md). Seven carry recorded
deviations rather than parity: process-scoped auto-generated machine keys (P07);
a codegen directory with no generation segment (P08); config map path selection
narrowed to the hosting map for all paths (P09); cache size sampling permanently
inactive (P12); a zero-seeded memory pressure history until the first collection
(P21); `IsHidden` classifying a different file set per OS (P23); and response
buffers that copy on send instead of transferring refcounted native ownership
(P28), with `bin` assemblies no longer shadow-copied (P30).

## Diagnostics

The runtime reports otherwise-discarded request exceptions on the
`Rehost.WebForms.Runtime` `EventSource`. The host attaches an `EventListener` at
`Error` level, so a passing run leaves standard error empty; raise the level from
a listener or `dotnet-trace` to also observe `bin` assembly resolution.

## Verify

```shell
dotnet src/PortableParity.Host/bin/Release/net10.0/PortableParity.Host.dll verify
```

Verification requires exact scenario, schema, ordered events, response,
escaped-exception shape, and completion counts against the committed Framework
`cold-sync.json`. Adapter-specific provenance is excluded. The checked-in
normalization manifest must exist and remain empty.

This exits zero. It runs automatically as part of `dotnet test
Rehost.WebForms.slnx`, which requires this prototype to have been built in
`Release` first.

Optional explicit inputs:

```shell
dotnet src/PortableParity.Host/bin/Release/net10.0/PortableParity.Host.dll verify \
  --expected ../framework-oracle/artifacts/golden/cold-sync.json \
  --normalization ../framework-oracle/metadata/normalization.json
```

The command intentionally exits nonzero at the current blocker. Do not add it
as an always-failing normal CI test until the reachable runtime slice advances.
