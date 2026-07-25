# First-slice pipeline fixture profile

Status: open. Priority: high. Depends on bootstrap/configuration.

## Problem

The product root baseline remains structurally Framework-derived. The
first-slice fixture needs isolation from unproven built-ins without redefining
product defaults.

## Required decisions

- Fixture-level `<clear/>` plus one probe module and precompiled handler;
  retain/probe the implicit `DefaultAuthenticationModule`.
- Which inherited Framework defaults are portable, disabled, deferred, or
  explicitly rejected in later slices.
- Failure timing and diagnostics for Windows authentication, impersonation,
  integrated pipeline, native health providers, and other excluded features.
- How the profile feeds the public compatibility map.

## Verification

Configuration tests prove the fixture clears inheritance and resolves its
handler/module through normal classic configuration. Product-baseline deltas
are tracked separately in the portability ledger.

## Done when

The fixture is deterministic without presenting its minimal registrations as
the product compatibility baseline.
