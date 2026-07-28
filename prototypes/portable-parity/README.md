# Portable core parity diagnostic

Infrastructure-only prototype. It targets `net10.0`; it is not referenced by
the production solution and does not change runtime behavior.

The portable and .NET Framework adapters share:

- one `netstandard2.0` session/observation contract;
- [`core-parity/sessions.json`](../core-parity/sessions.json), which declares
  every session both adapters run;
- the recording `HttpWorkerRequest` and request runner source;
- module and precompiled-handler probe source;
- the same application `web.config`;
- the empty normalization manifest and generated Framework golden trace.

A session is one process, one fixture, and an ordered list of steps. Each step
holds one or more named requests issued together, so a step of one is
sequential. The host spawns itself once per session and calls the existing
public sequence in that child:

```text
WebFormsApplication.Initialize
→ ApplicationManager.CreateObject
→ HttpRuntime.ProcessRequest(HttpWorkerRequest) per request, per step
→ StopObject, then drain the session notebook
```

Each request owns a notebook. Application-instance initialization is recorded as
an unordered bag, canonicalized by sorting, because instances are constructed on
overlapping threads; the shutdown notification and the instance count stay
ordered. Without that split, concurrent work would interleave one shared list
and no tape would reproduce. Probes find their own notebook from an
`X-Parity-Request` header the recording worker request carries, not from ambient
context.

Concurrency is held rather than hoped for: every request in a step waits until
the whole step has arrived, and the asynchronous handler finishes only after the
recorder reports that `ProcessRequest` returned. Both waits time out after five
seconds, so a runtime that serializes a step or completes the asynchronous
handler synchronously records an extra event instead of deadlocking.

A fresh process per session is what makes "cold" mean cold: nothing has touched
the `HttpRuntime` singleton, the default `AssemblyLoadContext` resolver, or the
activated application. Requests after the first in a session are warm by
construction.

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

The full classic pipeline runs. `verify` exits zero against an empty
normalization manifest: the portable trace matches the Framework golden exactly
across three sessions.

`cold-then-warm` serves a cold `201 Oracle Created`, a `CompleteRequest` that
skips the handler and still reaches `EndRequest`, a `202` whose handler finishes
after `ProcessRequest` returned, and three overlapping requests each echoing its
own identity from its own application instance. `concurrent-cold` starts two
requests together in a fresh process. `errors` uses a second fixture where a
module throws, a handler throws, and a handler type is absent; all three are
owned by System.Web error processing and return the same 3500-byte generic
error page, which carries no stack trace, exception type, or build footer and so
compares byte-for-byte across runtimes.

The configured `ProbeModule` and `SyncProbeHandler` resolve from
`fixture/app/bin` through classic configuration, with no host reference and no
preload. `DefaultAuthentication` appears after the cleared collection because
`HttpModulesSection.CreateModules` appends it, not because anything registers it.

This covers all eight scenarios in
[the first-slice parity gate](../../docs/adr/0031-require-the-first-slice-parity-gate.md)
on the differential side. The adapter side is covered by
[the adapter parity diagnostic](../adapter-parity/README.md), which replays the
same sessions over Kestrel against the same golden.

`PortableParityGateTests` in the main solution runs `verify` as a child process,
because running a session permanently mutates process-global state and cannot
share a process with other tests.

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

Verification requires exact schema, session and request names, session notebook,
ordered request events, response, escaped-exception shape, and completion counts
against the committed Framework `sessions.json`. Adapter-specific provenance is
excluded. The checked-in normalization manifest must exist and remain empty.

This exits zero. It runs automatically as part of `dotnet test
Rehost.WebForms.slnx`, which requires this prototype to have been built in
`Release` first.

Optional explicit inputs:

```shell
dotnet src/PortableParity.Host/bin/Release/net10.0/PortableParity.Host.dll verify \
  --expected ../framework-oracle/artifacts/golden/sessions.json \
  --normalization ../framework-oracle/metadata/normalization.json \
  --manifest ../core-parity/sessions.json
```

A single session can be run alone, which is also how the parent drives each
child:

```shell
dotnet src/PortableParity.Host/bin/Release/net10.0/PortableParity.Host.dll \
  run-session --session cold-sync
```
