# Parity rigs

Differential gates for the port: `sessions.json` declares the sessions, the
net481 oracle host generates the committed golden under `artifacts/golden/`,
and the portable and adapter hosts replay the same sessions against it. Shared
machinery lives in `src/Rehost.WebForms.Parity.Harness`.

Comparison rules live in the comparer (`TraceComparer`): strict for the
portable column, transport-aware for the adapter column. No normalization
layer exists, and none may be added; a fixture that would need one is a
design problem in the fixture.

Column details: [portable](README.portable-parity.md),
[adapter](README.adapter-parity.md), [oracle](README.framework-oracle.md).
