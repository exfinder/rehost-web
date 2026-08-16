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

- WAP sidecar build, publish payload, designer-file policy, and package-only
  consumption: [Web Site vs WAP](follow-ups/web-site-vs-wap-project-models.md).
- ~~Default document~~ (done: ledger P67) and remaining IIS request-path
  behavior: [IIS-role audit](follow-ups/iis-role-behaviors.md) and
  [IIS configuration layers](follow-ups/iis-integration-plan.md).
- Friendly URL behavior and escaping:
  [route URL escaping](follow-ups/route-url-escaping.md).
- ~~Restore the frozen template's physical script/style path~~ — done; the
  production combination/minification breadth of Web Optimization remains
  claimed only for the reached bundle/debug paths:
  [script-stack plan](follow-ups/stock-template-script-stack.md).
- ~~Ship the companion assemblies the root configuration names~~ — done: the
  [`Rehost.WebForms` metapackage](follow-ups/runtime-metapackage.md) owns the
  root configuration and its companions.
- ~~Restore static-asset behavior and mobile master/view switching~~ — done,
  pinned by `apps/WebFormsApplication/smoke.sh`.
- ~~Add one package-built browser journey on Windows x64, Linux, and macOS
  arm64~~ — done: `smoke.sh` in both `apps/*` folders, run on all three.
- A host-owned machine-key store so auto-generated keys survive restart and
  scale-out (the Identity template makes the process-scoped default visible as
  "every restart logs everyone out"); ASP.NET Core Data Protection's key ring
  is the candidate substrate: [machine key](follow-ups/machine-key-and-viewstate-bootstrap.md).

## Next

- Milestone 3 (production baseline): real SQL, deterministic Windows/Linux
  publish, containers, graceful lifecycle, readiness/health, metrics and
  diagnostics, external configuration and secrets, multi-instance operation —
  see [`../ROADMAP.md`](../ROADMAP.md).

## Later

### Hosting and request boundary

- Adapter residuals: response-spill exercise, server-variable inventory,
  configurable header encoding, client certificates, HTTP/2 and
  HTTP/3, streaming, compression/upgrades, and file-send surface:
  [host adapter](follow-ups/aspnet-core-host-adapter.md).
- Terminal-event and cancellation coverage:
  [request completion](follow-ups/request-completion-failure-and-cancellation.md).
- Abort the client connection when a timed-out synchronous step cannot return:
  [request termination](follow-ups/request-termination-and-timeouts.md).
- Slow-upload thread-pool cliff:
  [request-body concurrency](follow-ups/request-body-concurrency.md).
- ~~Path mapping and containment~~ — closed (ledger P57/P70–P73); boundaries
  recorded on [path mapping](follow-ups/portable-path-mapping-and-containment.md).
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
- Classify reached silent catches without creating diagnostic noise:
  [silent exceptions](follow-ups/silent-exception-swallowing.md).

### IIS-derived behavior and modules

- Per-folder IIS configuration, request filtering, custom headers, default
  documents, error shaping, handlers, and modules:
  [IIS configuration layers](follow-ups/iis-integration-plan.md) and
  [`system.webServer` mapping](follow-ups/system-webserver-configuration-compatibility.md).
- Classify and register or reject every Framework root `httpModules` entry:
  [shipped modules](follow-ups/shipped-http-modules.md).
- Replace catch-all/ignored behavior for omitted handlers and build providers
  with explicit diagnostics where the current request or compilation path hides
  an unsupported feature. Concrete case from production candidates: ReportViewer
  registers an `.rdlc` build provider and an `.axd` handler backed by a
  closed-source GDI+-bound assembly with no recompile path — that must surface
  as a named unsupported boundary, not a silent compilation or handler failure.
- Exercise `StaticFileHandler` `If-Range` behavior before widening the static
  file claim.

### State, security, and providers

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
- Persistent/shared machine keys, rotation, isolation suffixes, and fail-closed
  behavior: [machine key](follow-ups/machine-key-and-viewstate-bootstrap.md).
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

- Add default `ScriptManager` embedded-resource delivery through
  `ScriptResource.axd`, including protected URLs, debug/release selection,
  localization, caching, and deterministic generated Microsoft AJAX assets.
- Complete general `System.Web` embedded-resource delivery beyond the eight
  release scripts reached and embedded by the frozen template.
- Define general-consumer JS content deployment for
  `Rehost.WebForms.ScriptManager.Bundles`; its first slice registers names only
  and expects application-owned physical files.
- Port partial rendering (`UpdatePanel`, `UpdateProgress`, server `Timer`, and
  the async-postback wire protocol) when a milestone application reaches it.
- Keep PageMethods, AJAX ASMX/application services, JSON service plumbing,
  ListView/DataPager/query controls, LINQ-to-SQL, Dynamic Data, Entity, WCF/Data
  Services build providers, Windows-coupled Client Services, and design-time
  stacks outside the initial Extensions compile closure.
- Adopting that slice ends at WCF. Dropping the `Handlers` and `Script.Services`
  stubs pulls the real `WebServiceData`, whose `typeof(ProfileService)`,
  `typeof(AuthenticationService)`, and `typeof(RoleService)` name
  `[ServiceContract]` types from `System.ServiceModel`, which modern .NET does
  not host, and a `typeof` cannot be stubbed around. Everything short of that is
  reachable: measured, the remainder is roughly 25 imported files, a per-assembly
  `Res` table, a five-value `TransactionOption`, and one unused
  `using System.Web.Util` in `WebMethodAttribute`.
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

## Rejected

- Secondary AppDomains, remoting, CAS enforcement, and in-process static-state
  reset.
- Native IIS integrated-pipeline machinery as a runtime profile.
- Silent fallback for configured but unavailable behavior.
- Broad API completion without a milestone application or demonstrated
  consumer need.
