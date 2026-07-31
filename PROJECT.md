# Rehost WebForms

## Goal

Rehost classic ASP.NET Web Forms applications on modern .NET with minimal
application-source changes. Preserve observable `System.Web` behavior while
replacing dependencies on IIS, .NET Framework hosting, remoting, CAS, and
Windows-only services.

Behavioral compatibility is the primary goal: semantics and observable
behavior matter more than matching source shape for its own sake. A future
consumer must never be surprised by behavior the port introduced that legacy
`System.Web` did not have. Where the target platform cannot preserve some
original behavior or logic, first recover the original intent—from Reference
Source, documented rationale, or observed .NET Framework 4.8.1 behavior—and
replicate that intent as closely as the portability contract allows, rather
than substituting new logic.

Compatibility claims are separate:

- source/API compatibility;
- page, control, pipeline, configuration, and provider behavior;
- third-party control compatibility after recompilation;
- binary identity compatibility.

Source compatibility is necessary but not sufficient; behavioral fidelity
takes precedence over it. Binary interchangeability with Microsoft's
strong-named `System.Web` is not promised.

## Architecture

- Microsoft Reference Source remains the implementation baseline.
- `Rehost.WebForms.Runtime` owns the System.Web-compatible runtime.
- Hosting integration belongs behind host-neutral boundaries, principally
  `HttpWorkerRequest` and lifecycle services.
- Application Services and Web Services use sibling assemblies where their
  original assembly boundaries matter.
- One Web Forms application runs per OS process, in the current AppDomain and
  at full trust. Process replacement—not secondary AppDomains—provides restart
  and isolation.
- Runtime compilation remains in-process. Precompilation should be a separate
  build-time or process-level facility.

## Portability contract

The supported runtime is cross-platform. A feature is not considered supported
if it requires Windows, IIS, WindowsDesktop, the registry, COM, or DPAPI.

When portable behavior is infeasible, reject the feature on every platform
with actionable diagnostics. Do not create a Windows-only runtime profile or a
silent no-op.

## Compatibility policy

- Preserve public shape and observable behavior only where supported by tests.
- Treat unsupported behavior as an explicit contract.
- When original behavior cannot be reproduced, replicate the recovered
  intent as closely as the portability contract permits; document the
  residual gap instead of silently diverging.
- Separate source compatibility from assembly/binary identity.
- Require differential tests for intentional behavior changes.
- Keep security-sensitive compatibility opt-ins explicit. Legacy serialization
  and resource payloads are trusted-input features, not security boundaries.
- Never run compatibility tests or generated-output cleanup against a source
  checkout; copy fixtures to disposable directories.

## Authorities

Use, in order:

1. .NET Framework reference assemblies for public API shape.
2. A .NET Framework 4.8.1 runtime for observable behavior.
3. Pinned Microsoft Reference Source for implementation.
4. Current official .NET sources for modern implementation patterns.
5. Mono or earlier prototypes only as design evidence.

Source revisions, licenses, and transformations live in
[`docs/provenance`](docs/provenance/). Imported source should remain unchanged
where practical. Any imported-source deviation must be narrow, explained by a
compatibility decision, and covered by tests.

The Microsoft Reference Source and WinForms repositories are cloned locally at
`../referencesource` and `../winforms`, alongside this repository.

## Documentation policy

Code and tests are canonical for implementation mechanics. Documentation keeps
only current contracts, non-obvious rationale, authoritative provenance, and
active risks. Historical build counts and implementation chronology belong in
Git history.

Start with [`docs/README.md`](docs/README.md). Active work is indexed under
[`docs/follow-ups`](docs/follow-ups/).
