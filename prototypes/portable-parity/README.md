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

Current diagnostic status is platform-specific:

- macOS activation fails during `ApplicationManager` static initialization;
  the inner `PlatformNotSupportedException` reports unavailable Windows ACL
  resource-management APIs;
- Windows advances further, then activation fails loading native
  `webengine4.dll`.

These are unresolved portability blockers, not accepted deviations or skipped
tests.

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
