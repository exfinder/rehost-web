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
| Explicit application identity and physical/virtual roots | supported | all | Single process binding; invalid inputs fail during `WebFormsApplication.Initialize`. [Contract](../application-bootstrap-and-configuration.md) |
| Portable machine/root-web configuration baselines | supported | all | Versioned files copied to host `configs`; absolute overrides supported. Missing, inaccessible, or malformed inputs fail preflight. [Contract](../application-bootstrap-and-configuration.md) |
| Multiple applications or repeated bootstrap per process | unsupported | all | Later/concurrent initialization throws `InvalidOperationException`; process replacement required. [Contract](../application-bootstrap-and-configuration.md) |
| Partial trust and legacy CAS | unsupported | all | Bootstrap rejects non-`Full` trust or `legacyCasModel="true"`. [Contract](../appdomain-remoting-cas-compatibility.md) |
| Configuration watching/in-process reload | unsupported | all | Bootstrap rejects enabled FCN; configuration immutable until restart. [Follow-up](configuration-reload-and-process-restart.md) |
| Pre-application hooks during host startup | unassessed | all | `PreApplicationStartMethodAttribute` and `AppInitialize` verification deferred to runtime codegen/integration. |

## Done when

The first runnable request and every encountered IIS/Windows feature have
entries suitable for a future README compatibility section.
