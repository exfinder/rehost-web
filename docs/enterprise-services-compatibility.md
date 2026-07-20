# Enterprise Services compatibility

## Decision

Preserve the Framework transaction enum/API contracts while treating COM+
page transaction execution as explicitly unsupported on every platform.
Silent no-ops and mapping to `System.Transactions` were rejected because both
change transaction semantics.

`TransactionOption`, `TransactionVote`, and `ContextUtil` are internal Runtime
compatibility contracts under the original `System.EnterpriseServices`
namespace. No separate compatibility assembly is produced.

## Provenance and deviation

The enum names/values and transaction call sites come from .NET Framework 4.8
metadata and the pinned Microsoft System.Web Reference Source. The POC enums
confirmed the values but were internal and paired with silent `ContextUtil`
defaults; those choices were rejected.

The imported `Util/Transactions.cs` remains untouched but is excluded from the
Runtime compile. Its project-owned internal replacement preserves the call
shape needed by System.Web, always throws an actionable
`PlatformNotSupportedException`, and retains false-returning internal probes
used by `HttpServerUtility`.

This intentionally does not preserve the public type or assembly identity of
the Framework EnterpriseServices contracts. The current scope is System.Web's
internal use only; restore a dedicated assembly if external compatibility is
later required.

No COM+, IIS native transaction callback, CAS demand, performance counter, or
ambient transaction behavior is claimed.

## Validation and TODO

- xUnit v3 + Shouldly verifies internal visibility, enum metadata/values,
  explicit failure for disabled and required modes, callback non-execution,
  unchanged abort flag, and false fallback probes.
- Mandated Runtime build moves 14 errors to 8; warnings remain 1,083.
- Imported Reference Source is unchanged.
- TODO: once Runtime builds, exercise page transaction directive parsing.
- TODO: revisit a dedicated `System.EnterpriseServices` assembly if external
  contract compatibility becomes a requirement.
