# Application bootstrap and configuration

Status: reshape required in slice 1. Priority: highest. Canonical contract:
[managed runtime port plan](../core-runtime-port-plan.md).

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

Host registration is mutation-free and retryable after validation failure.
First-request activation is single-flight, traverses `ApplicationManager`, and
cannot expose partially initialized global state. Broad System.Web
configuration preflight, static `Initialize`, and catch-all bootstrap faulting
are removed.

Target contract:
[application bootstrap and configuration](../application-bootstrap-and-configuration.md).

Pipeline-native startup, pre-application hooks, and `Global.asax` verification
remain explicitly owned by the linked downstream stories in that contract.
