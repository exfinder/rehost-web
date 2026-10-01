# Rehost.Web

## Mission

Enable predominantly managed ASP.NET applications built on `System.Web` (Web
Forms, Web Pages, Web API, MVC) without intrinsic Windows or IIS dependencies to
rebuild and run on modern .NET across Windows, Linux, and macOS with minimal
application-source changes.

Preserve observable `System.Web` behavior where the portability contract permits.
When exact behavior cannot survive, recover the Framework intent, choose the
narrowest portable substitute, and state the boundary. This is not a promise of
complete `System.Web` coverage or binary interchangeability with Microsoft's
strong-named assembly.

## Architecture

- Microsoft Reference Source remains the implementation baseline.
- `Rehost.Web` owns the System.Web-compatible runtime.
- Hosting integration stays behind host-neutral request and lifecycle seams.
- Application Services and Web Services keep sibling assembly boundaries where
  their original identities matter.
- One application runs per OS process, in the current AppDomain, at full trust.
  Process replacement provides restart and isolation.
- Runtime compilation remains in-process. Precompilation is a separate
  build-time or process-level facility.
- Related managed Framework-era assemblies are ported only when a real
  application reaches them.

## Compatibility contract

- Behavioral compatibility outranks source-shape similarity.
- Source/API, behavior, third-party-after-recompile, and binary-identity claims
  are separate.
- Supported behavior must pass on Windows x64, Linux, and macOS arm64.
- A feature requiring Windows, IIS, WindowsDesktop, the registry, COM, or DPAPI
  is not part of the portable runtime.
- Infeasible behavior fails consistently with actionable diagnostics; it does
  not become a Windows-only profile or silent no-op.
- Framework and IIS observations decide uncertain application-visible behavior.
  The portable execution engine remains the classic managed pipeline through
  `HttpRuntime.ProcessRequest(HttpWorkerRequest)`.
- Security-sensitive compatibility remains explicit. Legacy serialization and
  resource payloads are trusted-input compatibility, not security boundaries.
- Application fixtures and generated output run from disposable copies, never a
  source checkout.

Current support claims and evidence live only in
[`docs/dev/compatibility.md`](docs/dev/compatibility.md).

## Engineering principles

- Explicit ownership over ambient discovery.
- Deterministic behavior across machines and operating systems.
- Validate before mutation; publish complete state atomically.
- Immutable application generations and a small lifecycle state machine.
- Fail near the source with actionable errors.
- Small public APIs with deep compatibility machinery behind them.
- Reuse authoritative parsers, validators, and runtime sequencing.
- Add narrow host-neutral seams; modify imported code surgically.
- Treat deployment assets, copy rules, and packaging as architecture.
- Record uncertain scope as backlog, never accidental partial support.

## Authorities

Use, in order:

1. .NET Framework reference assemblies for public API shape.
2. .NET Framework 4.8.1 and IIS for observable behavior.
3. Pinned Microsoft Reference Source for implementation.
4. Current official .NET sources for modern implementation patterns.
5. Mono and earlier prototypes only as design evidence.

Source revisions, licenses, and transformations live under
[`docs/dev/provenance`](docs/dev/provenance/). Imported source stays unchanged where
practical. A shipped 4.8.1 binary reading overrides an older published-source
snapshot when they measurably disagree.

## Direction

[`ROADMAP.md`](ROADMAP.md) owns milestones and priority. The project advances by
running increasingly representative applications, not by pursuing abstract API
completeness. [`docs/dev/backlog.md`](docs/dev/backlog.md) preserves all unresolved work.

## Documentation

Code and tests are canonical for mechanics. Documentation keeps current
contracts, non-obvious rationale, compatibility boundaries, authoritative
evidence, and unresolved work. Git keeps chronology.

Start at [Contributor documentation](docs/dev/README.md).
