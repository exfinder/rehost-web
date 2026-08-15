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
| `webserver` | `<system.webServer>` amendments over the shipped IIS baseline are the claim: removed and added extensions, extended and un-hidden segments, and an unhonored section tolerated. |
| `asyncapp` | An async `AddOnBeginRequestAsync` module event is app-global — every request through the host awaits it — which would perturb every other fixture's scenarios; hosts the truly-pending `IHttpAsyncHandler` beside it. |
| `friendlyurls` | Route-table registration is app-global, and later cache-mode scenarios mutate its file inventory. |
| `defdoc-disabled` | `<defaultDocument enabled="false" />` app-wide: directory requests must 403 with the courtesy redirect suppressed (readings D12/D14), so no directory URL in it can ever serve. |

## Host tenancy

The `page` fixture is runner-shared: every test class on `PageLiveScenario`
reaches one host process through `ScenarioHostRegistry` (an assembly fixture),
and `PageHostSharingTests` pins that contract. Classes on it must tolerate
other classes' requests interleaving:

- witness tokens come from `WitnessToken.For`, whose class-plus-method
  derivation keeps stage streams collision-free;
- the unfiltered witness readers (`EventsAsync`, `HandlerEntriesAsync`,
  `WaitForAsync`) return process-wide events — dedicated-host tools, never for
  a shared-host assertion;
- the shared façade exposes no trace view: the trace file cannot be
  filtered by class;
- no write into the application directory may change application startup
  behavior. Paths no other class reads are not enough: the hidden-segment
  probes seed directories like `App_Browsers/` that the port refuses at
  startup, benign only while file-change notification stays inert and the
  host never restarts.

Every other fixture keeps one host per test class. Single-tenant by necessity,
beyond the per-fixture conflicts above:

- `TimeoutSweepOverKestrelTests` runs a dedicated host over the `page` payload
  (`SweepLiveScenario`): its probe presents every registered request as
  expired, so any co-tenant's in-flight request would be spuriously timed out.
- `timeout`, `postback`, `body`, and the collection fixtures stay dedicated for
  the reasons in the table: their configuration or process-global claims are
  what is under test.
