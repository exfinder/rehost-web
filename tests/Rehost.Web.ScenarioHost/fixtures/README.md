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
| `body-customerrors` | `customErrors` conversion of host rejections is the claim. It also carries `<httpErrors errorMode="Custom">` with a `File` row onto `he/err404.json`, which replaces every 404 this host answers, and the directory refusal here is the 403 no row covers. Its `<clientCache>` puts `Cache-Control: public,max-age=60` on every static answer this host sends, `he/err404.json` served directly included. |
| `codegen` | Compilation-substrate scenarios: runs mutate and inspect generated output. |
| `legacy-target` / `modern-target` | `<httpRuntime targetFramework>` divergence is the claim; activation is what is under test. |
| `timeout` | `executionTimeout="1"` plus a 2-second `rehost:RequestTimeoutScanSeconds` would time out any other fixture's slow requests; the real-timer chain firing is the claim. |
| `webserver` | `<system.webServer>` amendments over the shipped IIS baseline are the claim: removed and added extensions, extended and un-hidden segments, an unhonored `<urlCompression>` section that warns at activation and blocks nothing, and `<customHeaders>` rows that put `X-Fixture` and `Cache-Control: no-store` on every response this host sends. It also carries the `<rewrite>` rule set, whose patterns all sit under `rw/` so no other class's request on this host meets one: `rw/clean/*` onto the probe page, `rw/hidden` and `rw/open` into the hidden and un-hidden folders, `rw/old` redirecting, `rw/dir` onto a folder with a default document, and `rw/bin/*` behind a segment request filtering denies. The `<requestLimits>` and `<verbs>` rows sit above what the other classes here use: 1000-byte bodies, 300-byte URLs, 200-byte queries, `DELETE` denied, and `maxUrlLength` raised past the URL limit so ASP.NET's own check does not answer before request filtering does. |
| `handlers` | `<handlers>` amendments that take the catch-all away: with `StaticFile` removed no static file, default document or unmapped URL in the application can ever serve, and a deliberately broken row 500s. No other fixture can host that. |
| `hookup-refused` | The `Global.asax` handler names a module event whose `add` accessor throws, so the application cannot build an instance and answers 500 to every request. Nothing else can be asserted on a host that never serves. |
| `defaultauth-refused` | Same shape one event further: the `Global.asax` handler names `DefaultAuthenticationModule.Authenticate`, whose `add` accessor refuses, so this application also answers 500 to every request. It cannot fold into `hookup-refused`, whose application already dies on a different event — one application can only prove which subscription killed it if it declares exactly one. |
| `asyncapp` | An async `AddOnBeginRequestAsync` module event is app-global — every request through the host awaits it — which would perturb every other fixture's scenarios; hosts the truly-pending `IHttpAsyncHandler` beside it. |
| `friendlyurls` | Route-table registration is app-global, and later cache-mode scenarios mutate its file inventory. Also hosts the OWIN module (`FixtureOwinStartup`, cookie authentication) — an app-global `HttpModule` that runs on every request — and the Web API 2 host (`WebApiConfig`, `App_Code` controllers, the session route handler swapped in by reflection from `Application_Start`). |
| `defdoc-disabled` | `<defaultDocument enabled="false" />` app-wide: directory requests must 403 with the courtesy redirect suppressed, so no directory URL in it can ever serve. |
| `session` | A `Session_Start` handler in `Global.asax` makes `SessionStateModule` retain otherwise unused new sessions. Every session-enabled request then stores a session and answers with `Set-Cookie`, so hosting these scenarios on `page` would add that header to every `.aspx` response there. |
| `session-custom` | `<sessionState mode="Custom" customProvider="…">` conflicts with the default InProc mode, and a `sessionState` section names exactly one mode, so it cannot share an application with the InProc scenarios. |
| `auth` | Forms authentication, roles and profile providers are app-global: every request through the host carries the authentication mode and provider set under test. |
| `autogen` | The shipped `AutoGenerate` machineKey default is the claim, so no literal keys may appear; every other auth-bearing fixture pins them. Its restart scenarios kill and restart the host process and delete the persisted key file. |
| `modules` | `<modules>` amendments are the claim, and the probe modules record a stage on every request in the application, which no other fixture's stage assertions could tolerate. |
| `modules-rammfar` | `runAllManagedModulesForAllRequests="true"` nullifies the `managedHandler` condition for the whole collection, which is the opposite claim to `modules`. |
| `migrated` | The surveyed production shape as one application: webServer-only modules including an auth gate that 403s a whole directory, `Session` swapped for a foreign type, and a handler `remove`/re-add. Every one of those is app-global. |
| `appstart` | `Application_Start` throws while a marker file outside the application is armed; the runtime latches the failure and ends the process. No other application can host a startup that fails. Activation and shutdown observations also belong here: values available to `Application_Start`, `Init()` and module `Init`, and stop signals raised by a host stop or recycle are written once per process and read after it ends. |
| `config-error` | Its `web.config` carries a section no `machine.config` declares, so activation fails in `HostingInit` and the application answers 500 to its one request before the runtime ends the process. Nothing else can be hosted on a configuration the runtime refuses to load. |
| `classic-unflagged` | Classic registrations without the validation flag: activation is refused, so this application can never serve a request. |
| `webpages` | ASP.NET Web Pages: `WebPageHttpModule` registers application-wide and reroutes every extensionless request to a matching `.cshtml` page, which the other fixtures' routing claims cannot tolerate. The three Web Pages assemblies ride in its bin, never beside the host, with `System.Web.Razor` staged beside them. |
| `mvc` | ASP.NET MVC 5: `Application_Start` registers the default `{controller}/{action}/{id}` route and an area route app-wide, so every extensionless URL no file serves reaches MVC, and `webpages:Enabled=false` at the root forbids every `.cshtml`; the `webpages` claims tolerate neither. Its compiled controllers (`Rehost.Web.ScenarioMvc`), `Rehost.Web.Mvc`, the Web Pages assemblies, `System.Web.Razor` and `Mindbox.Data.Linq` ride in its bin. `Views/Web.config` and `Areas/Admin/Views/Web.config` keep the original `System.Web.Mvc` and `System.Web.WebPages.Razor` names that fixture staging rewrites. |

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
  probes write into folders like `App_Code/` and `App_Browsers/` that the
  application compiles at startup, benign only because each file goes away
  before the next test, file-change notification stays inert and the host
  never restarts.
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
