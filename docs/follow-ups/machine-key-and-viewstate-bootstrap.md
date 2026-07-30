# Machine key and ViewState bootstrap

Status: open. Priority: high. Depends on security/persistence policy.

## Problem

Rendering a server form may initialize ViewState MAC and machine-key state.
Imported code assumes registry policy, native hashing, DPAPI-backed auto-gen
keys, and Windows identity/application isolation. Per-process random keys avoid
startup failure but break restart and scale-out behavior.

## Current state

The portable runtime takes Framework's random-key fallback (ledger P15).
Auto-generated keys are process-scoped:

- ViewState protected before a restart cannot be validated after it;
- forms-authentication tickets do not survive a restart;
- multiple processes cannot share tickets.

Explicit `<machineKey>` remains the stable restart/scale-out contract. A
host-supplied key source is not yet designed.

## `__VIEWSTATEGENERATOR` cannot agree with Framework

Found while scoping the page slice, and it decides one of the questions below.

`Page.GetClientStateIdentifier` combines
`StringUtil.GetNonRandomizedHashCode(TemplateSourceDirectory, ignoreCase:true)`
with the same over the generated page class name, and `Page.EndFormRender` writes
the result into the response as the `__VIEWSTATEGENERATOR` hidden field. So the
value is part of the rendered body of any page carrying `<form runat="server">`.

The two runtimes compute it differently **by design**. Ledger P38 removed the
`#if NETFRAMEWORK` shortcut to `string.GetHashCode()` and
`StringComparer.InvariantCultureIgnoreCase`, because .NET randomizes both per
process and every identity derived from them — the `CompilationLock` mutex name
among them — differed between processes. Framework keeps those branches and its
non-randomized BCL algorithms; this port always uses the stable algorithm the
method already carries. The two do not produce the same eight hex characters.

The divergence is permanent and cannot be normalized away under
[ADR 0029](../adr/0029-require-strict-differential-comparison.md). It is why the
slice-3 fixture carries no server form, and it means any later Framework
differential over a page with a form needs this recorded as an accepted
compatibility deviation before it can pass.

## Required decisions

- Whether the first fixture emits protected ViewState. The slice-3 page fixture
  does not: it carries no server form, so no `__VIEWSTATE` or
  `__VIEWSTATEGENERATOR` is rendered.
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
