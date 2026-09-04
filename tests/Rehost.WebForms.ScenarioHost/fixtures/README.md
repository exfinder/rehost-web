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
| `handlers` | `<handlers>` amendments that take the catch-all away: with `StaticFile` removed no static file, default document or unmapped URL in the application can ever serve, and a deliberately broken row 500s. No other fixture can host that. |
| `hookup-refused` | The `Global.asax` handler names a module event whose `add` accessor throws, so the application cannot build an instance and answers 500 to every request. Nothing else can be asserted on a host that never serves. |
| `asyncapp` | An async `AddOnBeginRequestAsync` module event is app-global — every request through the host awaits it — which would perturb every other fixture's scenarios; hosts the truly-pending `IHttpAsyncHandler` beside it. |
| `friendlyurls` | Route-table registration is app-global, and later cache-mode scenarios mutate its file inventory. Also hosts the OWIN module (`FixtureOwinStartup`, cookie authentication) — an app-global `HttpModule` that runs on every request. |
| `defdoc-disabled` | `<defaultDocument enabled="false" />` app-wide: directory requests must 403 with the courtesy redirect suppressed (readings D12/D14), so no directory URL in it can ever serve. |
| `session` | A `Session_Start` handler in `Global.asax` is app-global in a way that is easy to miss: it is one of the four conditions under which `SessionStateModule` *stops* discarding an unused new session (`SessionStateModule.cs:1285-1298`), so declaring it makes every session-enabled request in the application store a session and answer with `Set-Cookie` (reading S15). Hosting these tests on `page` would add that header to every `.aspx` response there permanently. Weaker than the other rows and recorded as such: measured against the suite as it stands, nothing breaks — the exact-cookie assertions run against probes, which take no session, and the one page-based cookie assertion is tolerant. |
| `session-custom` | `<sessionState mode="Custom" customProvider="…">` conflicts with the default InProc mode, and a `sessionState` section names exactly one mode, so it cannot share an application with the InProc scenarios. |
| `auth` | Forms authentication, roles and profile providers are app-global: every request through the host carries the authentication mode and provider set under test. |
| `autogen` | The shipped `AutoGenerate` machineKey default is the claim, so no literal keys may appear; every other auth-bearing fixture pins them. Its restart scenarios kill and restart the host process and delete the persisted key file. |
| `modules` | `<modules>` amendments are the claim, and the probe modules record a stage on every request in the application, which no other fixture's stage assertions could tolerate. |
| `modules-rammfar` | `runAllManagedModulesForAllRequests="true"` nullifies the `managedHandler` condition for the whole collection, which is the opposite claim to `modules`. |
| `migrated` | The surveyed production shape as one application: webServer-only modules including an auth gate that 403s a whole directory, `Session` swapped for a foreign type, and a handler `remove`/re-add. Every one of those is app-global. |
| `appstart` | Process death is the claim: `Application_Start` throws while a marker file outside the application is armed, the runtime latches the failure and ends the process, and the scenarios read the exit code. No other application can host a startup that fails. It carries the rest of the activation-and-shutdown record for the same reason — what `Application_Start`, `Init()` and module `Init` could read (IV22), and the stop signals a host stop and a recycle raise (IV30) — since both are written once per process and read after it ends. |
| `classic-unflagged` | Classic registrations without the validation flag: activation is refused, so this application can never serve a request. |

## Host tenancy

The `page` fixture is runner-shared: every test class on `PageLiveScenario`
reaches one host process through `ScenarioHostRegistry` (an assembly fixture),
and `PageHostSharingTests` pins that contract. Classes on it must tolerate
other classes' requests interleaving:

- stage reads go through `TracedGetAsync`, whose class-plus-method token keeps
  streams collision-free;
- the unfiltered witness readers (`EventsAsync`, `HandlerEntriesAsync`,
  `WaitForAsync`) return process-wide events, so only markers whose base class
  grants the whole-process witness (`Scenarios.cs`) can spell them;
- the shared façade exposes no trace view: the trace file cannot be
  filtered by class;
- no write into the application directory may change application startup
  behavior. Paths no other class reads are not enough: the hidden-segment
  probes seed directories like `App_Browsers/` that the port refuses at
  startup, benign only while file-change notification stays inert and the
  host never restarts.
- writes *beside* the application copy are the one sanctioned mutation:
  `ssi/` includes `../../shared/Banner.inc`, which `OutsideInclude.Write`
  places in the host's disposable root, outside the application, before the
  first `/ssi/` request; nothing else reads that directory.

Every other fixture keeps one host per test class. Single-tenant by necessity,
beyond the per-fixture conflicts above:

- `TimeoutSweepOverKestrelTests` runs a dedicated host over the `page` payload
  (`SweepLiveScenario`): its probe presents every registered request as
  expired, so any co-tenant's in-flight request would be spuriously timed out.
- `timeout` and the collection fixtures stay dedicated for the reasons in the
  table: their configuration or process-global claims are what is under test.
  `postback` and `body` are registry-shared like `page`.
