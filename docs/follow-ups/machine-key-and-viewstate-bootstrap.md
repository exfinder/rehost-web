# Machine key and ViewState bootstrap

The portable Framework-4.5+ algorithms and literal-key interoperability are
settled (ledger P15/P40/P48). Auto-generated keys now persist per application
([ADR 0010](../adr/0010-machine-key-persistence.md)): restart on one machine
keeps ViewState, Forms Authentication, and Katana cookies valid, and containers
or farms supply explicit keys through the `REHOST_WEBFORMS_MACHINEKEY_*`
environment variables or `<machineKey>`.

## Isolation boundary

`,IsolateApps` and `,IsolateByAppId` overwrite key bytes (legacy path) or add
derivation purposes (4.5 path) from an application hash. Framework and this
runtime deliberately use different stable hash algorithms (ledger P38/P48), so
even identical literal keys with either suffix cannot interoperate.
Mixed-runtime deployments must use bare keys.

## Remaining work

- Key rotation has no design: the one-key `MachineKeySection` model cannot
  express a ring, so rotating invalidates outstanding payloads. Revisit only
  with a real application need, together with
  [portable data protection](data-protection-provider.md).
- Encryption at rest for the key file (DPAPI equivalent) is deliberately
  absent; owner-only file permissions are the boundary.
- The non-HTTP `fNonHttpApp` branch in `MachineKeySection` stays
  process-scoped; it is unreachable for hosted applications.
