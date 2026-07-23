# Data protection provider

Status: open. Priority: high. Depends on security and persistence ADR.

Define a portable concrete `DataProtector` provider: key storage, rotation,
deployment sharing, purpose isolation, payload versioning, tamper handling, and
migration. Existing DPAPI payloads require Windows-only migration tooling that
decrypts and re-protects them.

Done when wire format/security properties are approved, differential tests
exist, restart/scale-out behavior is tested, and no Windows runtime dependency
is required.
