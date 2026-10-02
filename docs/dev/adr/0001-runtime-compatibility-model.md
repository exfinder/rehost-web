# Runtime compatibility model

## Decision

Use the retained classic managed pipeline as the portable execution engine.
Requests enter through `HttpRuntime.ProcessRequest(HttpWorkerRequest)` and keep
Framework sequencing through `HostingEnvironment`, `HttpRuntime`,
`BuildManager`, `HttpApplicationFactory`, modules, handler mapping, and
completion.

Framework 4.8.1 and IIS remain the behavioral oracle for application-visible
outcomes. `UseIntegratedPipeline` sites are decided individually: native
notification machinery stays excluded, while an app-visible behavior may need a
portable replacement matching integrated IIS observations.

Preserve imported control flow and break incompatible operations at narrow
leaves. Prefer, in order: a Framework-supported disabled configuration, an
existing inactive state, a surgical portable leaf preserving postconditions,
then explicit unsupported failure.

## Consequences

- Host code never selects handlers or recreates System.Web object lifetimes.
- `BuildManager` and normal configuration-driven handler/module resolution stay
  in the path even when a capability remains unsupported.
- IIS native modules, secondary-AppDomain management, and native integrated
  notification scheduling are not prerequisites.
- Compatibility boundaries expand only when executable application work reaches
  a new incompatible leaf.
- Broad imported-source rewrites require evidence that the retained sequence
  cannot satisfy the contract.
