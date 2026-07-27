# Machine key and ViewState bootstrap

Status: open. Priority: high. Depends on security/persistence policy.

## Problem

Rendering a server form may initialize ViewState MAC and machine-key state.
Imported code assumes registry policy, native hashing, DPAPI-backed auto-gen
keys, and Windows identity/application isolation. Per-process random keys avoid
startup failure but break restart and scale-out behavior.

## Current state

Reached by the request-ownership slice (ledger P15). `SetAutogenKeys` guards
only the machine-persisted lookup, so the portable runtime takes Framework's own
random-key fallback branch. Auto-generated keys are therefore **process-scoped**:

- ViewState protected before a restart cannot be validated after it;
- forms-authentication tickets do not survive a restart;
- two processes serving one application cannot share tickets, so multi-process
  deployment requires an explicit `<machineKey>`.

Explicit `<machineKey>` configuration is unaffected and remains the supported
way to obtain stable keys today. A system-wide or host-supplied `<machineKey>`
source is the expected direction; it is not yet designed.

## Required decisions

- Whether the first fixture emits protected ViewState.
- Portable MAC/encryption algorithms and compatibility requirements.
- Key source, isolation, persistence, rotation, deployment sharing, and file
  permissions.
- Registry policy defaults replaced by explicit Rehost configuration.
- Fail-closed diagnostics when secure key material is unavailable.

Coordinate with [data-protection-provider.md](data-protection-provider.md);
do not accidentally create two key-management systems.

## Verification

Focused crypto/key-lifecycle tests are required. Cross-restart, scale-out, and
Framework differential vectors may be staged and explicitly linked.

## Done when

First-page ViewState behavior has an approved security contract and no
registry, DPAPI, native crypto, or ephemeral-key surprise.
