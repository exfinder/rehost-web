# ASP.NET Core adapter parity diagnostic

Infrastructure-only prototype. It targets `net10.0` and exercises
[`Rehost.WebForms.Hosting`](../../src/Rehost.WebForms.Hosting), the production
adapter, over a real Kestrel server.

It shares with the two bench adapters:

- one `netstandard2.0` session/observation contract;
- [`core-parity/sessions.json`](../core-parity/sessions.json), which declares
  every session all three adapters run;
- module and precompiled-handler probe source;
- the same application `web.config`;
- the empty normalization manifest and generated Framework golden trace.

It does not share the recording `HttpWorkerRequest` or request runner. Requests
arrive over HTTP, so the adapter's own `AspNetCoreWorkerRequest` carries them.

## What it runs

The host spawns itself once per session and, in that child:

```text
AddRehostWebForms          → configuration validated, process claimed
UseRehostWebForms          → terminal middleware registered
Kestrel listens on 127.0.0.1:0
→ HTTP request per step, concurrent within a step
    → ApplicationManager.CreateObject on the first one only
    → HttpRuntime.ProcessRequest(AspNetCoreWorkerRequest)
→ ApplicationManager.CreateObject(AdapterRunner), StopObject, drain
→ StopAsync
```

A fresh process per session is what makes "cold" mean cold, and activation is
deferred to the first request so that request is the one that pays for it.

`AdapterRunner` exists because no response can show an application-wide fact.
It reaches the instance journal and the terminal shutdown notification through
the same registered-object mechanism the bench uses, which is why the adapter
needs no instrumentation inside the product.

## What is compared

Against the same committed Framework golden, unchanged:

- per-request probe events, in order;
- status code and reason phrase;
- response headers, as an unordered bag;
- response body bytes;
- application-instance initialization as a bag, and the ordered shutdown
  notification and instance count.

Skipped for this column, with the reason:

| Skipped | Why |
| --- | --- |
| `worker.*`, `runner.*` events | They mark moments inside the adapter that no response can show. Every value they announce — status, each header, body, flush, completion — is compared as a field. |
| `Flushes`, `EndOfRequestCount` | Not observable over HTTP. A second completion throws instead. |
| `Date`, `Server` headers | Kestrel adds `Date` unconditionally. `Server` is disabled at the listener and excluded here so a configuration slip reports as an extra header rather than passing silently. |
| Response header order | `HttpClient` does not preserve wire order. Order remains gate-checked on the bench column. |

The asynchronous claim survives the skips. `AsyncProbeHandler` writes its body
during `EndProcessRequest`, on a thread the pipeline has already released, so a
host that failed to await would deliver an empty body. Comparing the bytes is
the proof.

Concurrency is held rather than hoped for. `ParityBarrier.Begin` runs in the rig
before a step is dispatched and every request in that step waits at the handler
until the whole step arrives. The asynchronous handler parks on `ParityGate`
until the rig opens it, after the request is in flight. Both waits time out
after five seconds and record an extra event rather than deadlocking.

`CoreParity.Probes.dll` is staged only in `fixture/app/bin`. The host neither
references nor preloads it, and startup asserts both.

## Build

From this directory:

```shell
dotnet build AdapterParity.slnx -c Release
```

## Run

```shell
dotnet src/AdapterParity.Host/bin/Release/net10.0/AdapterParity.Host.dll run
```

## Verify

```shell
dotnet src/AdapterParity.Host/bin/Release/net10.0/AdapterParity.Host.dll verify
```

This exits zero. It runs automatically as part of `dotnet test
Rehost.WebForms.slnx`, which requires this prototype to have been built in
`Release` first.

A single session can be run alone, which is also how the parent drives each
child:

```shell
dotnet src/AdapterParity.Host/bin/Release/net10.0/AdapterParity.Host.dll \
  run-session --session cold-then-warm
```

## Current reachable state

All eight scenarios in
[the first-slice parity gate](../../docs/adr/0031-require-the-first-slice-parity-gate.md)
match the Framework golden on the adapter side, verified on macOS `arm64` and
Windows `x64`.

The request-body session matches the regenerated Framework golden on macOS
`arm64` and Windows `x64`.

Driving the real pipeline from a real server reached one platform edge no
differential probe had: `SafeNativeMethods.GetCurrentThreadId`, recorded as
[P33](../../docs/portability-ledger.md). The bench calls
`HttpRuntime.ProcessRequest` on a thread it owns, so nothing ever posted to the
application's synchronization context.

## Outside this prototype

Request-body timing, abort, and preload transport behavior has dedicated Kestrel
gates. File send, `PathInfo`, and client-visible streaming remain outside this
prototype.
