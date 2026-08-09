# Backlog

This is the complete unresolved-work index. `Current`, `Next`, `Later`,
`Parked`, and `Rejected` are the only priority buckets. Moving work between them
updates [`../ROADMAP.md`](../ROADMAP.md) when milestone scope changes.

## Current

Milestone 1 owns these outcomes:

- WAP sidecar build, publish payload, designer-file policy, and package-only
  consumption: [Web Site vs WAP](follow-ups/web-site-vs-wap-project-models.md).
- Default document and remaining IIS request-path behavior:
  [IIS-role audit](follow-ups/iis-role-behaviors.md) and
  [IIS configuration layers](follow-ups/iis-integration-plan.md).
- Friendly URL behavior and escaping:
  [route URL escaping](follow-ups/route-url-escaping.md).
- Port the template's managed dependency closure only as reached: Friendly
  URLs, Optimization/WebForms integration, ScriptManager assemblies,
  WebGrease, and their required dependencies.
- Restore bundle/script/static-asset behavior and mobile master/view switching.
- Add one package-built browser journey on Windows x64, Linux x64, and macOS
  arm64.

## Next

- Import eShopLegacyWebForms with frozen application sources and a sidecar host.
- Establish deterministic mock-data journeys before database integration.
- Recompile/replace only the managed dependency closure reached by eShop.
- Keep EF6 consumption separate from real SQL deployment, which belongs to
  Milestone 3.

## Later

### Hosting and request boundary

- Adapter residuals: response-spill exercise, server-variable inventory,
  `PathInfo`, configurable header encoding, client certificates, HTTP/2 and
  HTTP/3, streaming, compression/upgrades, and file-send surface:
  [host adapter](follow-ups/aspnet-core-host-adapter.md).
- Terminal-event and cancellation coverage:
  [request completion](follow-ups/request-completion-failure-and-cancellation.md).
- Abort the client connection when a timed-out synchronous step cannot return:
  [request termination](follow-ups/request-termination-and-timeouts.md).
- Slow-upload thread-pool cliff:
  [request-body concurrency](follow-ups/request-body-concurrency.md).
- Path containment, symlinks, encoded traversal, and Windows-shaped inputs:
  [path mapping](follow-ups/portable-path-mapping-and-containment.md).
- Cross-filesystem enumeration order, wildcard `bin` loading failures, and
  known-physical seams at the remaining reached call sites:
  [path mapping](follow-ups/portable-path-mapping-and-containment.md).
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
- Deterministic illogical `CallContext` isolation under pool-thread reuse:
  [call-context isolation](follow-ups/illogical-call-context-isolation.md).
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
  an unsupported feature.
- Exercise `StaticFileHandler` `If-Range` behavior before widening the static
  file claim.

### State, security, and providers

- InProc and custom session providers, then explicit boundaries for cookieless
  identity and `Session_End`: [session](follow-ups/session-state.md).
- SQL session schema/provisioning and two-host contention:
  [SQL session](follow-ups/session-sql.md).
- Capture and implement the native state-server client protocol:
  [state server](follow-ups/session-state-server.md).
- Persistent/shared machine keys, rotation, isolation suffixes, and fail-closed
  behavior: [machine key](follow-ups/machine-key-and-viewstate-bootstrap.md).
- Define worker identity and impersonation policy; explicitly refuse
  `<identity impersonate="true">` until a portable identity seam exists.
- Define Windows-authentication and native health-provider boundaries.
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
- Opportunistic test cleanup: migrate `MixedFarmOverKestrelTests` to act/assert,
  merge `ScenarioWorkerRequest` only if it becomes cheaper, and remove the
  ScenarioHost client half as probes migrate.

## Rejected

- Secondary AppDomains, remoting, CAS enforcement, and in-process static-state
  reset.
- Native IIS integrated-pipeline machinery as a runtime profile.
- Silent fallback for configured but unavailable behavior.
- Broad API completion without a milestone application or demonstrated
  consumer need.
