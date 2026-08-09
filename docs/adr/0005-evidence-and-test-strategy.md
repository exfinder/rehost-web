# Evidence and test strategy

## Decision

Use the cheapest evidence that can decide the claim.

Kestrel scenario tests are the primary path. A test acts against a passive
shared host and asserts the typed response first; server-side facts use a typed
journal or witness endpoint. A new fixture/process requires conflicting
configuration, cold activation, on-disk mutation, or process death. Process
trees stay one level deep.

Use a Framework differential only when the port replaces native/host-owned code
or must match a format not derivable from its inputs. The committed golden
remains a frozen regression gate. Prefer captured fixtures or ad-hoc
Framework readings for new evidence; do not create a golden session merely
because a new feature renders output.

Untouched imported behavior over already-exercised portable substrate may use a
recorded measurement plus the compatibility map. First reach of a
platform-sensitive leaf requires a portability-ledger row and a focused test on
the platform that triggers it.

## Consequences

- Golden values are generated, not hand-authored; comparison rules stay narrow
  and explicit.
- Windows is needed for exceptional oracle readings, not routine test work.
- Every solution project builds before `dotnet test --no-build`; tests derive
  their configuration from their own output path.
- Supported behavior passes on Windows x64, Linux x64, and macOS arm64.
- Day-to-day rules live in [writing tests](../writing-tests.md).
