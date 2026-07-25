---
status: accepted
---

# Derive root configuration from Framework

Replace the invented minimal root-web baseline with a structural derivative of
the pinned .NET Framework 4.8.1 root `web.config`.

Only required assembly-identity substitutions and evidence-backed portability
changes are allowed. Record every difference in the portability ledger.

The first-slice test application explicitly clears inherited handler and module
registrations, then adds its probe handler and module. This keeps the slice
deterministic without claiming support for every inherited Framework component.
