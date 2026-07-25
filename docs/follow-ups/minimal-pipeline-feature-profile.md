# Minimal pipeline feature profile

Status: open. Priority: high. Depends on bootstrap/configuration.

## Problem

Default Framework configuration can activate Windows authentication,
impersonation, health providers, session, file monitoring, and IIS-integrated
modules before the fixture needs them. Silent no-ops make applications appear
supported while changing security or lifecycle behavior.

## Required decisions

- Minimal modules/handlers required for dynamic `.aspx`.
- Which defaults are portable, replaced, or explicitly rejected.
- Failure timing and diagnostics for Windows authentication, impersonation,
  integrated pipeline, native health providers, and other excluded features.
- How the profile feeds the public compatibility map.

## Verification

Configuration tests prove required handlers load and excluded features fail
early with stable messages. End-to-end validation may wait for the fixture.

## Done when

The minimal profile is sufficient for the fixture and contains no silent
security, identity, monitoring, or IIS compatibility downgrade.
