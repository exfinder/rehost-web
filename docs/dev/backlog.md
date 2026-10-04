# Development backlog

This index owns unresolved work and priority. Follow-ups own detailed contracts,
decisions and completion criteria; [compatibility](compatibility.md) owns support.
[ROADMAP](../../ROADMAP.md) owns milestone direction. Change it when milestone
scope changes. Priority buckets: Current, Next, Later, Parked, Rejected.

## Current

- Public alpha: [release scope and gates](plans/2026-09-05-2135-public-alpha-release-plan.md).
  The [GitHub milestone](https://github.com/exfinder/rehost-web/milestone/1) owns tasks and completion status.
- Optional minimal WAP helper: [#21](https://github.com/exfinder/rehost-web/issues/21).

## Next

### Production baseline

- Deterministic Windows/Linux publish and containers for a representative application.
- Graceful lifecycle, readiness, health, metrics and diagnostics: [process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md).
- External configuration, secrets and multi-instance operation.
- SQL operations beyond schema creation and seeding; SQL-provider journey: [forms authentication](follow-ups/forms-authentication.md).
- Next representative application after the production baseline, selected by ROADMAP.

## Later

### Hosting and request boundary

- IIS configuration scope, rewrite remainder, native authorization, verb responses and caching: [IIS configuration tenants](follow-ups/iis-integration-plan.md).
- IIS wire canonicalization, filtering switches, compression and remaining native roles: [IIS-role behaviors](follow-ups/iis-role-behaviors.md).
- HTTP/3, proxied certificates and native server variables: [host adapter](follow-ups/aspnet-core-host-adapter.md).
- Terminal events, cancellation and failures: [request completion](follow-ups/request-completion-failure-and-cancellation.md).
- Connection abort for a timed-out synchronous step: [request termination](follow-ups/request-termination-and-timeouts.md).
- Slow-upload thread-pool behavior: [request-body concurrency](follow-ups/request-body-concurrency.md).
- Sent-header error pages and exception-text redaction: [request diagnostics](follow-ups/portable-request-diagnostics.md).
- Advanced Friendly URL escaping and rewrite mapping: [route URL escaping](follow-ups/route-url-escaping.md).
- `Server.TransferRequest` and extensionless-handler child-request semantics.
- Static-file `If-Range` behavior before widening support.
- Accept a physical path under the application root at the other path-taking sites (`Control.OpenFile`, `MapPathSecure`, `MailDefinition`, data sources, site maps), as `Response.WriteFile`/`TransmitFile` already do, once an application reaches one.
- Explicit diagnostics for unsupported build providers, including closed-source GDI+-bound ReportViewer dependencies.

### Lifecycle and platform boundaries

- Instance-owned application lifecycle, retriable registration, admission, drain, disposal and explicit host-owned asset paths: [process lifetime](follow-ups/process-lifetime-shutdown-and-recycle.md).
- Immutable configuration and file-change restart causes: [configuration reload](follow-ups/configuration-reload-and-process-restart.md).
- Remove remaining compiled `AppDomain.Unload` and broad warning suppression: [unload call sites](follow-ups/appdomain-unload-call-sites.md).
- Thread-pool, request-admission, cache-trim and recycle ownership: [runtime process policy](follow-ups/runtime-process-policy.md).
- Classify Windows reachability before removing analyzer suppression: [Windows diagnostics](follow-ups/windows-platform-diagnostics.md).
- Feed or refuse reachable environment-derived statics: [ambient statics](follow-ups/ambient-statics-audit.md).

### State, security and providers

- SQL membership, roles and profiles through a real `aspnetdb` application: [forms authentication](follow-ups/forms-authentication.md).
- Expiry `Session_End` and cookieless identity: [session](follow-ups/session-state.md).
- SQL session schema and multi-host contention: [SQL session](follow-ups/session-sql.md).
- Native state-server protocol: [state server](follow-ups/session-state-server.md).
- Machine-key rotation and encryption at rest: [machine key](follow-ups/machine-key-and-viewstate-bootstrap.md).
- Portable worker identity and impersonation policy.
- Port or substitute WIF session/federation and its machine-key crypto requirements.
- Windows authentication and native health-provider boundaries.
- Extend mixed Framework/port view state only for a real multi-node consumer.
- Portable protection format and key management: [data protection](follow-ups/data-protection-provider.md).
- Compiler, provider, batching and reproducibility policy: [compiler policy](follow-ups/compiler-provider-and-target-framework-policy.md).
- Precompiled deployment consumers and producers: [precompilation](follow-ups/precompiled-deployment.md).
- Embedded-resource identity without file-backed assemblies: [resource timestamps](follow-ups/web-resource-assembly-timestamps.md).

### Companion assemblies and client assets

- Type-forwarding facades so Framework-compiled libraries bind unmodified (high priority): [Framework assembly facades](follow-ups/framework-assembly-facades.md).
- General embedded-resource and ScriptManager physical-script deployment.
- Enabled application services, async error handling and excluded Extensions stacks: [Extensions](follow-ups/extensions-ajax-activation.md).
- Optimization bundle caching, `VaryBy` breadth and request handling.
- WebGrease image assembly/spriting, whose drawing dependencies are outside JS/CSS minification.

### Build system and packaging

- Audit-driven migration scaffolding and template package selection: [migration tooling](follow-ups/migration-tooling.md).
- WAP publish payload, designer files and Web Site packaging: [project models](follow-ups/web-site-vs-wap-project-models.md).
- Edit-markup-refresh without a rebuild: [in-place development](follow-ups/in-place-dev-run.md).
- ASP.NET Core coexistence, shared state and future layout choices: [migration stages](follow-ups/migration-stages-and-site-layout.md).
- Roslyn ReadyToRun for package consumers: [Roslyn packaging](follow-ups/roslyn-r2r-packaging.md).
- Clear activation failure when configuration names a missing companion in a partial deployment.
- C# application journey projects alongside App/Host, usable by external-consumer and CI runners.
- MVC variant of the `rehost-web` template.

## Parked

- Minimal App/Host MSBuild SDKs: [Rehost SDK](follow-ups/rehost-sdk.md).
- Container/cgroup memory validation: [container memory](follow-ups/container-memory-validation.md).
- Runtime metrics: [metrics](follow-ups/portable-runtime-metrics.md).
- Zero-copy response-buffer handoff: [buffer ownership](follow-ups/response-buffer-ownership.md).
- Generated regex/AOT policy: [regex generation](follow-ups/regex-generation.md).
- Generated-resource breadth: [generated resources](follow-ups/generated-resource-compatibility.md).
- Legacy ResX payload/platform breadth: [ResX](follow-ups/resx-compatibility.md).
- Enterprise Services directives/APIs: [Enterprise Services](follow-ups/enterprise-services.md).
- ASMX/SOAP breadth: [Web Services](follow-ups/web-services.md).
- Optional Framework-oracle automation; keep executable golden fixtures and capture new behavior only when uncertain.
- Scenario worker/client simplification when it reduces infrastructure cost.
- CI matrix, runner configuration, timing, mutation checks and clock-heavy fixtures: [test-suite infrastructure](follow-ups/test-suite-infrastructure.md).
- Dynamic IIS caching only for a public application without session cookies: [IIS configuration tenants](follow-ups/iis-integration-plan.md).
- Cache/output-cache providers and `VaryBy*` breadth.
- LINQ/Entity/Dynamic Data and typed DataSet scope.
- Visual Basic runtime page and App_Code compilation.

## Rejected

- Secondary AppDomains, remoting, CAS enforcement and in-process static reset.
- Native IIS integrated-pipeline machinery as a runtime profile.
- Silent fallback for unavailable configured behavior.
- Broad API completion without a representative application or demonstrated need.
