# Extensions AJAX activation plan

Agreed 2026-08-21 (interview over
[system-web-extensions-portability](../research/system-web-extensions-portability.md)).
Brings the rest of `System.Web.Extensions` into the port the way
`System.Web.Services` landed: compile everything portable, exclude only the
blocked stacks, migrate the Framework configuration closure, prove the wire.

## Decisions

1. **Scope: T1 + T2.** Compile `Handlers/ScriptModule.cs`, the internal JSON
   application services (`Profile/ProfileService.cs`,
   `Security/{Authentication,Role}Service.cs`), their sibling event-args and
   `KnownTypesProvider`/`WebServiceErrorEvent` files, and the query stack
   (`ui/WebControls/Expressions/*`, `QueryExtender`, `QueryableDataSource*`,
   `ContextDataSource*`, `DynamicData/*` contracts, `Dynamic.cs`, the
   portable helper/event-args files). Backlog with recorded triggers:
   `LinqDataSource` (needs `System.Data.Linq`), `.svcmap`/`.datasvcmap` WCF
   proxy codegen, `.svc` application-service hosting, Client Services.
2. **Exclusion mechanics: the Web Services bundle.** Excluded files stay out
   of the compile list (absent types fail consumer compilation; no stubs).
   Modified imported files keep originals behind `#if NETFRAMEWORK`,
   insertion-only; every touched file is a provenance deviation. Unsupported
   entry points reachable from compiled code throw actionable
   `PlatformNotSupportedException`. `Dynamic.cs` is bridged without
   modification: a Rehost-owned extension-method shim maps
   `AppDomain.DefineDynamicAssembly` to the static
   `AssemblyBuilder.DefineDynamicAssembly`.
3. **Configuration: all four Framework registrations migrate verbatim** from
   the golden configs, Rehost assembly names, exact Framework names/flags —
   the `system.web.extensions` section group into
   `rehost-webforms.machine.config`; `ScriptModule-4.0`,
   `*_AppService.axd → ScriptHandlerFactory` (`validate="False"`), and the
   `System.Web.UI.WebControls.Expressions` `<pages><controls>` entry into
   `rehost-webforms.web.config`. The module name stays exactly
   `ScriptModule-4.0` (applications carry `<remove>` against it).
4. **Framework defaults restored:** the `#if NETFRAMEWORK` gate in
   `Script/Services/WebServiceData.cs` is reverted — its recorded reason
   ("WCF-hosted") is disproven; the built-in `*_JSON_AppService.axd` names
   map to the internal script services as on Framework. The Extensions
   deviation list shrinks to the `ScriptControlManager.cs` ambiguity fix.
5. **Deployment: no new promises.** All new registrations name
   `Rehost.WebForms.Extensions`, already guaranteed by the metapackage and
   `tests/BaselineNamedAssemblies.props`; config edits ship through the
   existing configs glob.
6. **Evidence bar:** full protocol bar plus one browser-verified sample.

## Stages (straight to main, every commit builds and tests green)

1. Satellite compile-list changes: extend the Extensions csproj to the T1+T2
   closure, add the `AssemblyBuilder` shim, revert the `WebServiceData.cs`
   gate, update provenance.
2. Baseline config wiring: the four registrations.
3. Scenario fixtures and tests on the shared `PageLiveScenario` host
   (~15–22 facts, ~6–8 C#5-only fixture files doubling as the IIS oracle
   site): UpdatePanel async postback, async/postback triggers, error format
   (customErrors off/on), redirect, Timer tick, page methods
   (POST/GET/auth), JSON application services over in-memory providers
   (login/logout/isLoggedIn, roles, profile), QueryExtender. Zero new host
   processes intended; if provider configuration measurably disturbs the
   shared host, that is the fixtures-README structural-isolation decision,
   surfaced with a recorded reason.
4. `samples/Rehost.WebForms.SampleApp`: an UpdatePanel demo page — sample
   only, no tests targeting it; one real-browser pass (panel updates without
   reload, console clean) gates the story.
5. Wire readings on winbox full IIS (identical fixture pages under 4.8):
   async-postback family, one page method, one application service, diffed
   byte-for-byte with volatile headers normalized. Known delta classes:
   `Environment.NewLine`, serializer attribute ordering, exception-ToString
   text, plus ICU-vs-NLS culture payloads where globalization is emitted.
6. 3-OS rounds (macOS suite, `eng/linux-round.sh`, Windows round), then docs
   closure: compatibility rows, an Extensions compatibility doc, provenance,
   and this file rewritten to hold only the deliberate backlog.

## Standing rules

- Stop and ask the moment implementation contradicts an agreed decision.
- First-reach failures of dormant environment-derived statics get explicit
  bootstrap-owned seams, recorded as provenance deviations
  ([ambient-statics-audit](ambient-statics-audit.md)).
- No comments by default; findings go to commit messages, provenance, docs.
