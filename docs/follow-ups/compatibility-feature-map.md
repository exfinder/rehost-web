# Compatibility feature map

Status: open. Priority: high. Depends on explicit support decisions.

## Goal

Maintain a precise user-facing map of source/API and behavioral compatibility.
For each feature record: supported, partially supported, unsupported, or
unassessed; platforms; security/trust constraints; failure mode; and owning
contract/test.

## Rules

- “Supported” means portable tested behavior.
- Nothing is supported only on Windows.
- Partial support names the exact boundary.
- Unsupported behavior fails explicitly where reachable.
- Unassessed is not equivalent to unsupported or supported.
- Story completion updates the map; history stays in Git.

## Current map

| Feature | Status | Platforms | Contract/failure |
| --- | --- | --- | --- |
| Explicit application identity and physical/virtual/work roots | unassessed | all | Mutation-free registration and single process binding are slice-1 work. [Contract](../application-bootstrap-and-configuration.md) |
| Framework-derived machine/root-web configuration baselines | unassessed | all | Versioned assets and every portable delta require provenance and tests. [Contract](../application-bootstrap-and-configuration.md) |
| Multiple published applications per process | unsupported | all | One immutable generation per process; replacement is external. [Contract](../application-bootstrap-and-configuration.md) |
| Partial trust and legacy CAS | unsupported | all | Normal runtime configuration consumption rejects non-`Full` trust or `legacyCasModel="true"`. [Contract](../appdomain-remoting-cas-compatibility.md) |
| Configuration watching/in-process reload | unsupported | all | Hosting/configuration disables FCN; configuration remains immutable until process replacement. [Follow-up](configuration-reload-and-process-restart.md) |
| Pre-application hooks during host startup | unassessed | all | `PreApplicationStartMethodAttribute` and `AppInitialize` verification deferred to runtime codegen/integration. |

## Done when

The first runnable request and every encountered IIS/Windows feature have
entries suitable for a future README compatibility section.
