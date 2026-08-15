# Machine key and ViewState bootstrap

## Settled by the postback slice

- **Portable algorithms are not a decision.** P40 refuses any application below
  `targetFramework` 4.5, so `MachineKeySection.CompatibilityMode` is always at
  least `Framework45` and `AspNetCryptoServiceProvider.IsDefaultProvider` is
  always true. That path is pure managed BCL — SP800-108, `HMACSHA256`, `AES` —
  and nothing under `Security/Cryptography` carries a registry, DPAPI, or
  `NETFRAMEWORK` branch. The output-buffer-size hash trap recorded below sits in
  `MachineKeySection.EncryptOrDecryptData`, reachable only below `Framework45`
  or through the obsolete `MachineKey` API, and is therefore unreachable rather
  than merely unported.
- **The first fixture does emit protected view state.** The postback fixture
  carries a server form and declares an explicit `<machineKey>`.
- **Auto-generated keys are now reported.** Preflight raises one
  `WebFormsRuntimeEventSource` event naming the process-scoped consequence
  (ledger P15). It is a diagnostic only; nothing observable to a client changes,
  and the host-supplied key source below remains the actual fix.

- **A shared literal key interchanges view state with Framework.** Payloads
  rendered on either runtime are accepted by the other, which is what a farm
  spanning both requires. The scope and the untested edges are in
  [compatibility map](../compatibility.md#state-security-and-ancillary-assemblies);
  the requirement it places here is that key sharing has a consumer depending on
  it, not only a fixture.

Storage, rotation, deployment sharing, file permissions, and fail-closed
behavior remain open and stay coordinated with
[data-protection-provider.md](data-protection-provider.md).

## `,IsolateApps` desynchronizes even an explicit key

Not yet a ledger row: no slice executes the branch, so by the ledger's own rule
it stays here until one does.

`MachineKeySection.RuntimeDataInitialize` strips a `,IsolateApps` or
`,IsolateByAppId` suffix and then overwrites the first four bytes of the
resulting key with `StringUtil.GetNonRandomizedStringComparerHashCode(appName)`.
It does this for a **literal hex key**, not only for `AutoGenerate`. That helper
is the same P38 divergence as `__VIEWSTATEGENERATOR`: Framework computes
`StringComparer.InvariantCultureIgnoreCase.GetHashCode`, this port computes
`GetStringHashCode(s.ToLower(InvariantCulture))`. Both are stable; they are not
equal.

So two runtimes given byte-identical configuration derive different keys, and
neither can read view state or forms-authentication tickets the other produced —
silently, with no diagnostic, for a suffix that is part of the shipped default
value of both `validationKey` and `decryptionKey`. Any deployment that must
interoperate with .NET Framework has to drop the suffix, which is why the
postback fixture declares bare keys.

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

The Identity template makes the default visible: Katana's cookie middleware
protects `.AspNet.ApplicationCookie` through `MachineKey.Protect`, so every
process restart logs every user out until the application declares a literal
`<machineKey>`.

## Candidate substrate: ASP.NET Core Data Protection

Framework's persistence for auto-generated keys was the worker process's
DPAPI-protected registry store under the pool identity — one master key per
identity, per-application isolation applied at load time by `IsolateApps`
(virtual path) or `IsolateByAppId`. `System.Security.Cryptography.ProtectedData`
is Windows-only on .NET, so DPAPI is not the portable answer.

`Microsoft.AspNetCore.DataProtection` ships in the shared framework the host
already runs on and is Microsoft's cross-platform replacement for exactly this
pattern: a persisted key ring (per-user directory by default on every OS, or an
explicit directory, Redis, blob, database), optional at-rest protection (DPAPI
on Windows, X.509 elsewhere), `SetApplicationName` for isolation, and rotation.
The narrow design question is which layer it feeds:

- **key material only** — the ring supplies the master bytes that
  `MachineKeySection` treats as its auto-generated key, keeping every ASP.NET
  wire format (view state, forms tickets, `MachineKey.Protect`) unchanged and
  the `IsolateApps` derivation intact; restart and scale-out stability follow
  from sharing the ring;
- **protector directly** — an `IDataProtectionProvider` handed to Katana in
  place of `MachineKeyDataProtectionProvider`, which changes the cookie
  payload format and leaves view state on the old path.

The first keeps one crypto model and is the recommended starting point; neither
makes keys interchangeable with a Framework machine, since the `IsolateApps`
hash already differs by design (P38/P48).

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

The divergence is permanent and cannot be normalized away under the
[evidence strategy](../adr/0005-evidence-and-test-strategy.md). It is why the
slice-3 fixture carries no server form. It is now recorded as ledger P48, and a
Framework differential over a page with a form compares the field's shape rather
than its value.

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
