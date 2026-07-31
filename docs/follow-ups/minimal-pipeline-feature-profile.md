# First-slice pipeline fixture profile

Status: resolved for slice 1.

## Problem

The product root baseline remains structurally Framework-derived. The
first-slice fixture needs isolation from unproven built-ins without redefining
product defaults.

## Settled boundary

- Fixtures clear inherited handlers and modules, register one probe module and
  precompiled handler, and retain the implicit `DefaultAuthenticationModule`.
- Product defaults remain Framework-derived; reached defaults are classified in
  the portability ledger and compatibility map.
- Windows authentication, impersonation, integrated pipeline, native health
  providers, and other excluded features remain later feature boundaries.

## Verification

Configuration tests prove the fixture clears inheritance and resolves its
handler/module through normal classic configuration. Product-baseline deltas
are tracked separately in the portability ledger.

## Result

The fixture is deterministic without presenting its minimal registrations as
the product compatibility baseline.
