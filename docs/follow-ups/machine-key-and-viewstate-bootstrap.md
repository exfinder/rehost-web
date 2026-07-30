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

## The legacy path selects its hash algorithm by output-buffer size

`ObjectStateFormatter` protects view state through `AspNetCryptoServiceProvider`,
which is pure managed BCL, whenever `machineKey compatibilityMode` is
`Framework45` or later — which `<httpRuntime targetFramework="4.5" />` or later
sets, and which the Visual Studio project template has emitted for years. Below
that it takes `MachineKeySection.EncryptOrDecryptData`, whose hashes are native.
The obsolete `MachineKey.Encode`/`Decode` reach the same native hashes on any
application, regardless of compatibility mode.

Those hashes carry a trap worth recording before anyone ports them. Native
`GetSHA1Hash` and `GetHMACSHA1Hash` selected the algorithm from the **output
buffer length** they were handed, which is `_HashSize`, not from the SHA1 the
names claim. `_HashSize` follows `validation`: 16 for `MD5`, 20 for `SHA1`,
`AES` and `3DES`, then 32, 48, and 64 for `HMACSHA256`, `HMACSHA384`, and
`HMACSHA512`. A mechanical port to `SHA1.HashData` and `HMACSHA1.HashData`
compiles, runs, and computes the wrong MAC — on the default path, since 4.5
defaults `validation` to `HMACSHA256`.

The sibling `Portable.System.Web` POC hit exactly this and spent hours on a view
state decryption mismatch. Its first test suite passed while the code was wrong,
because it validated SHA1 against RFC 2202 and NIST SHA1 vectors: the right
answer to the wrong question. Only baselines taken from `webengine4.dll` itself
exposed it. Ledger P41 refuses the native path outright, so this is a cost to
weigh if pre-4.5 support is ever wanted, not a live defect.

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
