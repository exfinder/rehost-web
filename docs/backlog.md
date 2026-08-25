# Backlog

This is the complete unresolved-work index. `Current`, `Next`, `Later`,
`Parked`, and `Rejected` are the only priority buckets. Moving work between them
updates [`../ROADMAP.md`](../ROADMAP.md) when milestone scope changes.

## Current

Milestone 1 (both Visual Studio templates on all three platforms) is complete;
its remaining detail moved below. Milestone 2 owns these outcomes:

- Import eShopLegacyWebForms with frozen application sources and a sidecar host.
- Establish deterministic mock-data journeys before database integration.
- Recompile/replace only the managed dependency closure reached by eShop.
- Keep EF6 consumption separate from real SQL deployment, which belongs to
  Milestone 3.

Carried from Milestone 1 as unresolved detail:

- WAP publish payload, designer-file policy, package-only consumption, and
  secondary Web Site packaging: [project models](follow-ups/web-site-vs-wap-project-models.md).
- Remaining IIS request-path behavior: [IIS-role audit](follow-ups/iis-role-behaviors.md) and
  [IIS configuration layers](follow-ups/iis-integration-plan.md).
- Friendly URL behavior and escaping:
  [route URL escaping](follow-ups/route-url-escaping.md).

## Next

- Milestone 3 (production baseline): real SQL, deterministic Windows/Linux
  publish, containers, graceful lifecycle, readiness/health, metrics and
  diagnostics, external configuration and secrets, multi-instance operation —
  see [`../ROADMAP.md`](../ROADMAP.md).

## Later

### Hosting and request boundary

- Adapter residuals: HTTP/3, proxied client certificates, integrated-mode
  server variables: [host adapter](follow-ups/aspnet-core-host-adapter.md).
- Terminal-event and cancellation coverage:
  [request completion](follow-ups/request-completion-failure-and-cancellation.md).
- Abort the client connection when a timed-out synchronous step cannot return:
  [request termination](follow-ups/request-termination-and-timeouts.md).
- Slow-upload thread-pool cliff:
  [request-body concurrency](follow-ups/request-body-concurrency.md).
- Response diagnostics and failure preservation:
  [request diagnostics](follow-ups/portable-request-diagnostics.md).

### Lifecycle and platform boundaries

- Align hosting with the lifecycle ADR: instance-owned application API,
  mutation-free retriable registration, explicit state/admission publication,
  runtime-to-host terminal notification, drain, and disposal. Remove ambient
  `AppContext.BaseDirectory/configs` discovery in favor of explicit host-owned
  asset paths.
- Drain, disposal, explicit unload, restart, and host replacement:
  [process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md).
- Remove the remaining compiled `AppDomain.Unload` and broad warning
  suppression: [unload call site](follow-ups/appdomain-unload-call-sites.md).
- Immutable-config change detection and restart signaling:
  [configuration reload](follow-ups/configuration-reload-and-process-restart.md).
- Thread-pool, request-admission, cache-trim, and recycle ownership:
  [runtime process policy](follow-ups/runtime-process-policy.md).
- Remove broad Windows analyzer suppression after reachability classification:
  [Windows diagnostics](follow-ups/windows-platform-diagnostics.md).
- Classify the dormant environment-derived statics in
  `HttpRuntime`/`HttpConfigurationSystem`/`HostingEnvironment`; feed or
  fail-fast the ones with reachable consumers:
  [ambient statics audit](follow-ups/ambient-statics-audit.md).
- Classify reached silent catches without creating diagnostic noise:
  [silent exceptions](follow-ups/silent-exception-swallowing.md).

### IIS-derived behavior and modules

- Integrated-mode divergences the [round-4 readings](research/iis-modules-handlers-readings.md)
  measured and this runtime has not closed:
  - `system.webServer/security/authorization` is not read at all. An application
    that protected a folder with the native section — the only one of the two that
    reaches a static file (MH36) — gets no protection here and no error.
  - `OPTIONS` answers 405 where IIS answers 200 with an `Allow` header and an
    obsolete `Public` carrying the same list, and `TRACE` answers 405 where IIS
    answers 501 unless `EnableTraceMethod` is set (MH37, MH38).
  - `<fileExtensions>` is still read from the application root alone, where IIS
    resolves it per path (MH32). The rest of that section is honored: ledger P86.
- `staticContent`, `defaultDocument` and `hiddenSegments` are still
  merged from the application root alone and are unassessed per folder;
  `fileExtensions` is measured there and is the entry above.
- Request-filtering limits, custom headers, and error shaping — the tenants the
  modules/handlers work did not touch:
  [IIS configuration layers](follow-ups/iis-integration-plan.md) and
  [`system.webServer` mapping](follow-ups/system-webserver-configuration-compatibility.md).
- `Server.TransferRequest` and the extensionless-URL handler's child-request
  semantics. The baseline carries the `ExtensionlessUrlHandler-Integrated-4.0`
  row and dispatch is transparent through it (ledger P85), but the API itself
  is deferred; friendly URLs are the expected trigger.
- Classify the remaining shipped registrations against reached behavior:
  [shipped modules](follow-ups/shipped-http-modules.md). The golden's module and
  handler rows now register wholesale, so what is open is feature-level support
  behind them, not registration.
- Replace catch-all/ignored behavior for omitted build providers with explicit
  diagnostics where the current compilation path hides an unsupported feature.
  Concrete case from production candidates: ReportViewer registers an `.rdlc`
  build provider and an `.axd` handler backed by a closed-source GDI+-bound
  assembly with no recompile path — that must surface as a named unsupported
  boundary, not a silent compilation failure. A handler row whose type will not
  load already fails its own URLs with the entry named (MH22a).
- Exercise `StaticFileHandler` `If-Range` behavior before widening the static
  file claim.

### State, security, and providers

- Prove Microsoft's SQL membership/role/profile providers against a real
  `aspnetdb` through an `apps/` sample on all three operating systems; the
  modules, provider defaults, Windows-mode refusal, and the sign-in journey
  over fake providers have landed:
  [forms authentication](follow-ups/forms-authentication.md).
- `Session_End` on expiry, and cookieless identity: InProc and custom providers
  are delivered, so what remains is the cache sweep thread reaching the expiry
  callback, and `UseUri`/`AutoDetect`/`UseDeviceProfile`:
  [session](follow-ups/session-state.md).
- SQL session schema/provisioning and two-host contention:
  [SQL session](follow-ups/session-sql.md).
- Capture and implement the native state-server client protocol:
  [state server](follow-ups/session-state-server.md).
- Let configuration name an ADO.NET provider again, which modern
  `System.Data.Common` dropped along with the `<system.data>` handler; reaches
  `SqlDataSource`'s `ProviderName`, and today every non-SqlClient provider is
  host code (`WebFormsIdentityApplication` registers SQLite that way):
  [provider factories](follow-ups/db-provider-factories-config.md).
- Machine-key rotation and key-file encryption at rest (persistence and
  environment keys landed, [ADR 0010](adr/0010-machine-key-persistence.md)):
  [machine key](follow-ups/machine-key-and-viewstate-bootstrap.md).
- Define worker identity and impersonation policy; explicitly refuse
  `<identity impersonate="true">` until a portable identity seam exists.
  Observed in production candidates as likely-vestigial config beside
  `authentication mode="None"`, so the refusal diagnostic should name the
  setting and the removal fix.
- Decide port-or-substitute for `System.IdentityModel.Services` (WIF session
  and federation): Framework-only, never carried to modern .NET, yet
  production candidates register `SessionAuthenticationModule` and
  `MachineKeySessionSecurityTokenHandler`, and third-party SAML modules
  depend on it transitively; any answer drags `machineKey` crypto
  compatibility with it.
- Define Windows-authentication and native health-provider boundaries
  (`WebProcessInformation` is portable since ledger P75; providers and
  `HealthMonitoringSection` rules remain unassessed).
- Extend mixed Framework/port view-state evidence beyond the one captured root
  page only if a real multi-node consumer needs it.
- Portable protection wire format and key management:
  [data protection](follow-ups/data-protection-provider.md).
- Remaining compiler/provider, batching, resource, and reproducibility policy:
  [compiler policy](follow-ups/compiler-provider-and-target-framework-policy.md).
- Precompiled consumer and producer deployment:
  [precompilation](follow-ups/precompiled-deployment.md).
- `WebResource` identity without file-backed assemblies:
  [resource timestamps](follow-ups/web-resource-assembly-timestamps.md).

### System.Web companion assemblies and client assets

- Complete general `System.Web` embedded-resource delivery beyond the eight
  release scripts reached and embedded by the frozen template.
- Define general-consumer JS content deployment for
  `Rehost.WebForms.ScriptManager.Bundles`; its first slice registers names only
  and expects application-owned physical files.
- Remaining Extensions scope after the 2026-08-22 AJAX activation (enabled
  application services, `customErrors`-On async errors, debug/localized
  scripts, and the excluded LINQ-to-SQL/WCF/Client Services stacks):
  [Extensions](follow-ups/extensions-ajax-activation.md).
- Validate production Optimization combination, caching, request handling, and
  minification separately from the frozen template's debug-mode expansion.
- Assess WebGrease image assembly/spriting separately; those paths use legacy
  drawing/desktop types and are not covered by JS/CSS minifier execution.

### Build system and packaging

- `Rehost.WebForms.Sdk` MSBuild SDK package for minimal consumer csproj files:
  [Rehost SDK](follow-ups/rehost-sdk.md).
- In-place development run via a host-provided bin seam, restoring the
  edit-markup-refresh loop: [in-place dev run](follow-ups/in-place-dev-run.md).
- Choose the project license and set `PackageLicenseExpression`; nuget.org
  publishing is blocked until then:
  [package license](follow-ups/package-license.md).
- Roslyn ReadyToRun delivery for package consumers (today only in-repo hosts
  get R2R Roslyn): [Roslyn R2R packaging](follow-ups/roslyn-r2r-packaging.md).
- Decide whether a runtime-plus-Extensions bundle is useful beside the
  template-shaped metapackage, and fail activation clearly when configuration
  names an absent companion assembly.

## Parked

- Container/cgroup memory validation:
  [container memory](follow-ups/container-memory-validation.md).
- OpenTelemetry-shaped runtime metrics:
  [runtime metrics](follow-ups/portable-runtime-metrics.md).
- Zero-copy response-buffer handoff:
  [buffer ownership](follow-ups/response-buffer-ownership.md).
- Generated regex/AOT policy: [regex generation](follow-ups/regex-generation.md).
- Generated resource differential breadth:
  [generated resources](follow-ups/generated-resource-compatibility.md).
- Legacy ResX payload/platform breadth:
  [ResX](follow-ups/resx-compatibility.md).
- Enterprise Services directive/API scope:
  [Enterprise Services](follow-ups/enterprise-services.md).
- ASMX/SOAP scope: [Web Services](follow-ups/web-services.md).
- Framework-oracle CI automation. Current integration tests are primary; keep
  the frozen golden gate, prefer captured fixtures or ad-hoc Framework readings,
  and do not recreate historical experiments unless evidence is missing or
  contradictory.
- Opportunistic test cleanup: merge `ScenarioWorkerRequest` only if it becomes
  cheaper, and remove the ScenarioHost client half as probes migrate.
- Test-suite process infrastructure, deliberately deferred from the 2026-08
  harness refactoring: a three-OS CI matrix for the routine suite (the
  Framework oracle stays exceptional, above), committed runner configuration
  (parallelism and hung-test detection are xUnit defaults today), a suite
  timing record with a budget, periodic mutation runs over port-owned seams,
  and shrinking `TimeoutOverKestrelTests`' real-clock wait (~7s) if the sweep
  contract allows a shorter fixture timeout.
- General cache/output-cache breadth beyond basic fragment caching: providers
  and `VaryBy*` policy remain unassessed
  ([map](compatibility.md#state-security-and-ancillary-assemblies)).
- Legacy declarative data stack: `LinqDataSource`, `EntityDataSource`, Dynamic
  Data, and typed DataSets remain absent or unassessed; `.xsd` build-provider
  generation fails explicitly
  ([map](compatibility.md#compilation-and-pages)).
- Visual Basic runtime page/App_Code compilation; only C# is supported today
  ([map](compatibility.md#compilation-and-pages)).

## Rejected

- Secondary AppDomains, remoting, CAS enforcement, and in-process static-state
  reset.
- Native IIS integrated-pipeline machinery as a runtime profile.
- Silent fallback for configured but unavailable behavior.
- Broad API completion without a milestone application or demonstrated
  consumer need.
