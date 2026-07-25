# Classic-path portability ledger

Status: seed. Update only as an executable slice reaches each edge.

This is not a dependency encyclopedia or assumed truth. Each row records one
postcondition on the supported classic path. Reference Source explains the
edge; an oracle probe decides observable semantics.

Treatment:

- **keep** — unchanged managed behavior;
- **config-disable** — normal Framework configuration selects inactive behavior;
- **inactive** — existing initialized object naturally has no capability;
- **portable leaf** — retain callers/order; replace only the platform operation;
- **unsupported** — fail explicitly when selected;
- **deferred** — later compatibility slice.

State:

- **decision** — architecture selected, implementation/probe pending;
- **research** — exact treatment still requires evidence;
- **verified** — implementation and required probe pass.

## Activation and hosting

| ID | Edge and required postcondition | Legacy/platform dependency | Treatment | Proof | State |
| --- | --- | --- | --- | --- | --- |
| A01 | registration normalizes immutable identity and roots without global mutation | IIS/application ambient identity | portable leaf | invalid/corrected registration tests | decision |
| A02 | application `bin` can resolve an unreferenced managed handler assembly | AppDomain application base/private-bin probing | portable leaf | handler exists only in fixture `bin` | decision |
| A03 | `.appDomain`, `.appId`, `.appPath`, `.appVPath`, `.domainId` publish as one usable binding | child-AppDomain data | portable leaf | partial binding injection; no consumer sees it | decision |
| A04 | `ApplicationManager` per-app lock/context and object creation execute | default-domain manager/remoting | keep | activation trace includes manager/cache path | decision |
| A05 | application environment exists without child AppDomain | `AppDomain.CreateDomain`, setup, remoting | portable leaf | one current-domain environment; secondary-domain API rejects | decision |
| A06 | `HostingEnvironment.Initialize` receives explicit host/map/config inputs | IIS app host/config token | portable leaf | mapped configuration and path probe | decision |
| A07 | lifecycle object is registered and receives `Stop` | cross-AppDomain registered object | portable leaf | exactly-once shutdown probe | decision |
| A08 | normal classic hosting flags remain active | POC shortcut flags | keep | trace flags; AppInitialize eligibility | decision |
| A09 | FCN and ACL-read capability are authoritatively inactive | Windows directory notifications/ACL reads | config-disable | inactive object/postcondition tests | decision |

## `HttpRuntime` construction and hosting initialization

| ID | Edge and required postcondition | Legacy/platform dependency | Treatment | Proof | State |
| --- | --- | --- | --- | --- | --- |
| H01 | static runtime construction is allowed on every supported OS | unconditional Win32 gate | portable leaf | static-init probe on each OS | decision |
| H02 | timeout manager and request/completion callbacks exist | managed | keep | private postcondition/failure tests | decision |
| H03 | worker identity field has deterministic supported meaning | Windows process identity | research | consumer trace and oracle comparison | research |
| H04 | performance-counter calls preserve control flow with inactive counters | native performance counters/web engine | inactive | null/inactive counter probes | decision |
| H05 | `FileChangesMonitor` object exists with FCN disabled | Windows FCN/registry defaults | config-disable | timestamp/path consumers still work | decision |
| H06 | `DataDirectory` and application-directory checks complete | AppDomain data and Windows ACL assumptions | portable leaf | path/access/error probes | research |
| H07 | directory/bin monitor phase preserves postconditions while inactive | Windows change notifications | config-disable | no watch handles; initialized state | decision |
| H08 | object-cache host initializes before configuration consumption | managed/static hosting state | keep | ordering trace | research |
| H09 | minimal cache/trust/security/compilation/hosting sections are read in Framework order | mapped System.Configuration | keep | config access/order/error trace | decision |
| H10 | codegen directory is deterministic, writable, and generation-specific | runtime install dir/AppDomain dynamic directory/ambient temp | portable leaf | work-root and conflict probes | decision |
| H11 | prefetch phase cannot reach native Windows API | native prefetch | config-disable or inactive | effective-config and reachability probe | research |
| H12 | runtime publishes full trust and rejects partial trust/CAS at normal consumption point | CAS policy/AppDomain permission set | unsupported | exact config diagnostic and timing | decision |
| H13 | fusion/private-bin postcondition loads application assemblies | AppDomain fusion/shadow-copy APIs | portable leaf | A02 plus dependency resolution probe | decision |
| H14 | URL metadata and global configuration completion retain order | managed configuration/cache | keep | ordering and lazy-section probe | research |
| H15 | process/thread-pool policy does not silently retune shared host state | ASP.NET process model/native policy | config-disable or portable leaf | before/after process-policy probe | research |
| H16 | autogen/machine-key phase produces deterministic supported state | legacy machine/process secrets | keep or later security leaf | key initialization probe | research |
| H17 | `BuildManager.InitializeBuildManager` executes | AppDomain/codegen assumptions below it | keep | initialization trace | decision |
| H18 | resource perf-counter phase leaves valid inactive state | native counters | inactive | no native reachability | research |
| H19 | pre-application methods and `App_Code.AppInitialize` retain order | generated compilation/loading | deferred | slice-2 oracle trace | decision |

## First request and classic pipeline

| ID | Edge and required postcondition | Legacy/platform dependency | Treatment | Proof | State |
| --- | --- | --- | --- | --- | --- |
| R01 | public `HttpRuntime.ProcessRequest` guards, counters, queue, and admission execute | none for classic managed entry | keep | entry trace; prove no `ProcessRequestNow` shortcut | decision |
| R02 | `HttpContext`, `HttpRequest`, and `HttpResponse` are created from one worker request | IIS worker-request details | portable leaf | core and Kestrel mapping probes | decision |
| R03 | `FirstRequestInit` executes once using first real context | lazy static state | keep | concurrent cold-request probe | decision |
| R04 | application enabled/offline behavior retains request error/status semantics | filesystem/application file | keep or portable leaf | oracle cases | research |
| R05 | request queue initializes without hidden IIS/process tuning | thread/process heuristics | research | concurrency/admission trace | research |
| R06 | health monitoring heartbeat has explicit inactive or supported state | timers/providers/event log | config-disable | no provider/timer reachability | research |
| R07 | tracing initializes from normal configuration | diagnostics/perf integration | keep with inactive leaves | trace-disabled and enabled probes | research |
| R08 | IIS folder ACL restriction phase cannot mutate platform ACLs | IIS/Windows ACL | inactive via hosting flag | phase postcondition/no ACL calls | decision |
| R09 | bin preload cannot become ambient recursive probing | impersonation/fusion | application-bin resolver | app-bin dependency probe | decision |
| R10 | encoder and request validator initialize at deterministic point | managed/configured types | keep | first-request ordering/failure trace | research |
| R11 | factory lazy initialization and `Global.asax` semantics remain | dynamic compilation/FCN | deferred | slice-2 oracle trace | decision |
| R12 | configured modules plus implicit `DefaultAuthenticationModule` are created per pooled `HttpApplication` and disposed with it | reflection/config | keep | inventory/pooling/lifetime trace | decision |
| R13 | configured handler mapping/factory selects precompiled handler | BuildManager/config type resolution | keep | mapped handler fixture | decision |
| R14 | normal `HttpApplication` pooling and per-request `HttpContext` isolation remain | managed pool/static current context | keep | warm concurrent probe | decision |
| R15 | module short-circuit still reaches `EndRequest` | event/callback pipeline | keep | `CompleteRequest` trace | decision |
| R16 | sync and delayed async handlers unwind through normal callbacks | `IAsyncResult` callbacks | keep | paired sync/async oracle traces | decision |

## Completion, failure, and shutdown

| ID | Edge and required postcondition | Legacy/platform dependency | Treatment | Proof | State |
| --- | --- | --- | --- | --- | --- |
| C01 | managed module/handler errors use System.Web formatting | worker-response implementation | keep | error response and event trace | decision |
| C02 | only exceptions escaping request entry fault adapter completion | host task bridge | portable leaf | handled-versus-escaped probe | decision |
| C03 | `EndOfRequest` completes exactly once | native completion callback | portable leaf | sync, async, failure race tests | decision |
| C04 | disconnect is observable but does not abandon pipeline ownership | IIS disconnect callback/Kestrel cancellation | portable leaf | disconnect-before-async-complete test | decision |
| C05 | first-slice response preserves logical headers/fragments/order | native response elements/synchronous Kestrel stream | portable leaf | spool and final commit tests | decision |
| C06 | large spooled output uses owned generation work storage and cleans up | unmanaged buffers/ambient temp | portable leaf | spill, failure, disposal tests | decision |
| C07 | cached initialization failure can produce System.Web error page | AppDomain restart timer | keep plus shutdown leaf | initialization-error oracle trace | decision |
| C08 | AppDomain shutdown request notifies owner once | AppDomain unload/recreate | portable leaf | concurrent/repeated shutdown test | decision |
| C09 | owner requests Kestrel stop; replacement remains external | IIS/WAS recycle | portable leaf | fake host-lifetime probe | decision |
| C10 | graceful drain, `Application_End`, disposal, and `Stopped` | AppDomain unload coordination | deferred | lifecycle-slice gate | decision |

## Ledger update rule

For each implementation change:

1. select the reached row;
2. link exact Reference Source callers and consumers;
3. state the required postcondition before choosing treatment;
4. add the oracle/core/adapter probe identifier;
5. record any normalization or accepted deviation;
6. mark **verified** only after all required OS jobs pass.

If evidence changes a decision, update the ADR and ledger together. Do not
preserve a row merely because it was written first.
