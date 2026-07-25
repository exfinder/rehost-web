# Machine key and ViewState bootstrap

Status: open. Priority: high. Depends on security/persistence policy.

## Problem

Rendering a server form may initialize ViewState MAC and machine-key state.
Imported code assumes registry policy, native hashing, DPAPI-backed auto-gen
keys, and Windows identity/application isolation. Per-process random keys avoid
startup failure but break restart and scale-out behavior.

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
