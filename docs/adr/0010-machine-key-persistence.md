# Machine-key persistence

Framework read auto-generated machine keys from a machine-wide store: WAS
generated 1024 bytes once per worker identity, DPAPI-protected them into that
identity's registry hive, and `SetAutogenKeys` fetched them over ISAPI. None of
those leaves survives off Windows/IIS, so the port's auto-generated keys were
random per process and every restart invalidated ViewState, forms tickets, and
Katana cookies (ledger P15).

The port persists them instead: one key file per application, read and created
by `AutogenKeyStore` through the same stored-keys seam `SetAutogenKeys` already
had for the ISAPI fetch. The file is exactly `s_autogenKeys`' 1024 bytes,
published by write-to-temp-then-rename so a concurrent starter reads either
nothing or a complete file, owner-only (0600/0700) on Unix.

## Identity and location

The file is keyed on a digest of the canonicalized application directory
(trailing separator trimmed, case folded on Windows only) — the same choice as
codegen storage (ADR 0008), so renaming the host `ApplicationId` keeps the
keys. It is deliberately not Framework's shared-per-identity master: Framework
separated applications with `,IsolateApps`, which hashes the virtual path, and
in this runtime nearly every application's virtual path is `/`, so a shared
master would give distinct applications identical effective keys. A per-app
master preserves the guarantee that matters — distinct applications never share
keys — and the isolation suffix derivation stays untouched on top.

The directory resolves to the host `MachineKeyDirectory` option, else
`~/.rehost-webforms/machine-keys` under the user profile — the portable
analogue of the per-identity registry hive, and outside both the reclaimable
temp root and the (possibly read-only, disposable) application directory.

## Engagement and failure

The store engages only when the effective `machineKey` declares `AutoGenerate`;
fully literal configurations never touch the key directory. Preflight resolves
and validates the store at boot, so an unreadable, unwritable, or wrong-length
file fails startup naming the file and which source supplied its directory.
There is no degraded fallback to process-scoped keys: Framework's equivalent
fallback branch effectively never ran under IIS, and its symptom (cookies die
at the next restart) surfaces far from its cause.

## Environment-supplied keys

Containers are the primary production target, and a container keeps neither
the user profile nor a writable web.config, so explicit keys arrive through
`REHOST_WEBFORMS_MACHINEKEY_VALIDATIONKEY` / `_DECRYPTIONKEY`. A variable
substitutes the whole attribute string before parsing — isolation suffixes
included — so it behaves exactly as the same value written in web.config; the
scenario proof is a forms-ticket interchange against a config-keyed host. A
variable applies only where the declared attribute auto-generates: an explicit
configured key alongside a set variable is conflicting ownership and fails
preflight, the same rule as `CompilationTempDirectory`. The variables are also
a pair: setting one without the other fails preflight naming the missing one —
a lone variable is almost always a missed or mistyped deployment setting, and
the symptom (half the payloads invalid) would surface far from the cause. Config builders were
rejected as the vehicle: the modern `System.Configuration.ConfigurationManager`
engine never received the Framework-4.7.1 `<configBuilders>` feature, and
recreating it means forking the config engine.

Both `MachineKeySection.RuntimeDataInitialize` and the Framework-4.5 crypto
path (`MachineKeyMasterKeyProvider`) read the substituted strings; the two
paths read the section independently, and fencing only one splits the crypto
model — the initial implementation did exactly that and the interchange test
caught it.

## Out of scope

Rotation (Framework never rotated autogen keys, and the one-key
`MachineKeySection` model cannot express a ring), encryption at rest (DPAPI is
Windows-only; owner-only file permissions are the portable substitute, as ASP.NET
Core Data Protection itself ships on Linux), cross-machine scale-out of
auto-generated keys (explicit keys or env vars, as on Framework), and the
non-HTTP `fNonHttpApp` branch, unreachable for hosted applications. The
Framework-interchange limits of the isolation suffixes are unchanged (ledger
P38/P48).
