# Application bootstrap and configuration

Status: done. Priority: high. Depends on host lifecycle/configuration boundary.

## Problem

Imported initialization expects IIS/AppDomain-populated application identity,
machine configuration, root web configuration, map-path services, and restart
behavior. Config templates located beside a host assembly are a deployment
accident, not a stable contract.

## Required decisions

- Required application identity and one-application-per-process binding.
- Host-owned physical/virtual roots and configuration sources.
- Portable baseline machine/root configuration and provenance.
- Initialization concurrency, idempotence, partial-failure rollback, and retry.
- Configured versus unsupported file-change/reload behavior.
- Actionable validation for missing, malformed, or inaccessible inputs.

## Verification

Test successful bootstrap, concurrent calls, conflicting identities, failure
before/after state mutation, retry policy, and configuration-source diagnostics.

## Done when

Bootstrap has no ambient IIS, registry, assembly-location, or secondary
AppDomain dependency and cannot expose partially initialized global state.

Implemented contract:
[application bootstrap and configuration](../application-bootstrap-and-configuration.md).

Pipeline-native startup, pre-application hooks, and `Global.asax` verification
remain explicitly owned by the linked downstream stories in that contract.
