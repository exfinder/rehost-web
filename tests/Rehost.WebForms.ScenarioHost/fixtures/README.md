# Scenario fixtures

A new Kestrel scenario joins an existing fixture's shared host unless its claim
requires isolation. A new fixture — like a new process — needs a structural
reason: conflicting configuration, a cold/activation claim, on-disk mutation,
or process death. Every fixture below carries its justification; additions must
meet the same bar.

| Fixture | Why it cannot fold into another |
| --- | --- |
| `page` | Byte-exact golden response (`Default.expected.html`); its `Default.aspx` conflicts with `postback`'s at the same path. |
| `postback` | Literal `machineKey` + pinned `__VIEWSTATEGENERATOR` (path-derived, so pages cannot move); serves postback and multipart probes from one host. |
| `farm` | Replays a payload captured from real .NET Framework (`Framework.postback`); its `Default.aspx` conflicts with `postback`'s. |
| `body` | `maxRequestLength`/`requestLengthDiskThreshold` limits are the claim; the spilled-content save probe needs that same low threshold. |
| `body-preload` | `asyncPreloadMode="All"` is a different application. |
| `body-customerrors` | `customErrors` conversion of host rejections is the claim. |
| `codegen` | Compilation-substrate scenarios: runs mutate and inspect generated output. |
| `legacy-target` / `modern-target` | `<httpRuntime targetFramework>` divergence is the claim; activation is what is under test. |
| `timeout` | `executionTimeout="1"` plus a 2-second `rehost:RequestTimeoutScanSeconds` would time out any other fixture's slow requests; the real-timer chain firing is the claim. |
