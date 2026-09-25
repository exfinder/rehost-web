# DNN Platform portability assessment

Preliminary static assessment of DNN Platform on Rehost WebForms. This is not
an import plan or support claim. No DNN build, installer, or application code
was executed. Any later baseline or probe must use a disposable copy, never the
source checkout. The [compatibility map](../compatibility.md) remains the sole
source of Rehost support claims.

## Source and assessment boundary

The official repository was cloned read-only for research at
`/Users/vm/repos/Dnn.Platform`. It did not exist beforehand. The assessment uses
the current stable release **v10.3.3**, commit
`b417ffad45522ed718edb72c0a5d77f72249690e`, released from `master`; the checkout
is detached at that tag. All upstream links below pin that exact commit.

The repository default branch was `develop`, at
`c5fc79a9d393388b67430cce88fe8f1650c5f005` when cloned (2026-09-04), 839
commits beyond v10.3.3. Its post-release work includes 10.4 schema/version work,
dependency updates, and early multi-targeting changes. Those changes are useful
future evidence but are not a released deployment unit. v10.3.3 is therefore
the reproducible initial target. A later implementation should rebase the
assessment only after a newer stable release exists.

Scope is the built-in platform, website, installer, administration UI, and a
single representative built-in Web Forms module. Optional authentication,
cloud, social, SMTP OAuth, export/import, MVC, SPA, Razor, and third-party
extensions are classified but excluded from the proposed first journey.

## Verdict

**Conditionally feasible, high effort, not ready to import.** DNN is a plausible
architecture-driving application for Rehost, but it is several application
bring-ups combined:

- a large net48 WAP and roughly 60 `System.Web`-bound projects that must be
  rebuilt against Rehost;
- a managed IIS module/handler pipeline whose core routing, authentication,
  dependency injection, and resource delivery run on every request;
- a SQL Server-backed installer and long ordered upgrade chain that mutates the
  database, configuration, and application tree;
- a multi-portal host/path router;
- a dynamically installed module/provider ecosystem; and
- a long tail of Windows-only or absent Framework assemblies.

There is no evidence of one irreducible blocker on the bounded Web Forms
journey. There are, however, three gates that should stop implementation until
approved:

1. **Deployment ownership:** run a preinstalled immutable DNN generation first,
   or support DNN's in-web-process installer/config/bin mutation model.
2. **Dependency scope:** accept a source rebuild of the reached DNN closure and
   selected third-party `System.Web` consumers; Microsoft `System.Web` binary
   identity cannot be reused.
3. **Product boundary:** make built-in Web Forms modules the first contract;
   defer MVC/Web API/Razor/SPA, Windows authentication, remote IIS
   administration, and arbitrary marketplace modules.

The recommended answer is: **approve an immutable, preinstalled, Web
Forms-only vertical slice before fresh-install compatibility.** It will test
the runtime surfaces most likely to invalidate the effort without first solving
DNN's deployment system.

## Application and repository shape

The solution is not one web project. The tagged tree contains 79 C# projects,
2 VB projects, and a solution with 146 project entries. Seventy-one projects
target net48, six target netstandard, and three are build/tooling projects on
net10. Sixty-one project files directly reference `System.Web*`; source imports
are much broader.

| Layer | Concrete shape | Initial disposition |
| --- | --- | --- |
| Website | Old-style net48 Web Application Project, IIS Express enabled, `System.Web`, ApplicationServices, DynamicData, Entity, Extensions, and Services references ([project](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Website/DotNetNuke.Website.csproj#L12-L17), [references](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Website/DotNetNuke.Website.csproj#L89-L94)) | Rebuild as frozen WAP `.App`; stage `Website` as site content; small Rehost `.Host` |
| Core | `DotNetNuke` Library plus `DotNetNuke.Web`, HttpModules, Web.Client, ResourceManager, Instrumentation, DI, CSP, Maintenance, WebUtility, WebControls | Rebuild reached closure; cannot consume binaries that bind Microsoft's `System.Web` |
| Built-in UI | Web Forms pages/user controls under Website plus separately built modules, providers, controls, and Persona Bar/Edit Bar projects | Start with one Web Forms module and required admin shell; defer unrelated modules |
| VB assemblies | `DotNetNuke.WebUtility.vbproj` and `DotNetNuke.WebControls.vbproj` | Rebuild with the modern VB compiler if their reached API closure permits; Rehost's unsupported *runtime VB page compilation* is a separate issue |
| Modern leaf libraries | Abstractions, DependencyInjection, Instrumentation and some tooling already target netstandard/net10 | Attempt consume/rebuild as-is only after reached-path probes |
| Website assets | `DesktopModules`, `admin`, `controls`, `Portals`, `Install`, `Providers`, resources, icons, JS, CSS and skins | Deployment payload, not incidental content |
| Packaging | Cake/net10 build orchestration, npm/yarn/lerna front-end work, manifest/package targets, generated install/upgrade/deploy artifacts | Reproduce outputs deterministically; do not make upstream build scripts part of runtime |

The Website directly references eleven sibling projects, including both VB
assemblies, HttpModules, the core Library, Web, CoreMessaging, and DDRMenu
([project closure](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Website/DotNetNuke.Website.csproj#L3303-L3354)). The core Library alone pulls
WebPages, Microsoft.Web.Infrastructure, Lucene, PetaPoco, mail, crypto, zip, and
sanitization packages plus the System.Web family
([packages](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Library/DotNetNuke.Library.csproj#L23-L41),
[references](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Library/DotNetNuke.Library.csproj#L44-L69)). This invalidates a strategy of
porting only `DotNetNuke.Website.dll`.

## Dependency classification

Classification is by likely reached behavior, not mere reference. Every
“consume” or “substitute” entry remains provisional until a net10 probe drives
the exact DNN path. The prior Wingtip result is the warning: a referenced Katana
Google middleware package looked plausible until startup constructed a
Framework-only type.

### Rebuild against Rehost

These assemblies bind `System.Web` or each other across the core request path:

- Website, `DotNetNuke` Library, `DotNetNuke.Web`, HttpModules;
- Web.Client and Client ResourceManager;
- WebUtility and WebControls;
- the selected Web Forms module and its direct DNN dependencies;
- DNN-specific data, caching, folder, HTML editor, and authentication providers
  reached by startup or the journey; and
- any third-party library whose reached binary references Microsoft's
  strong-named `System.Web` identity.

This is source/API compatibility after rebuild, not binary compatibility. The
Rehost map explicitly calls Microsoft `System.Web` binary identity unsupported
([map](../compatibility.md#application-and-deployment-model)).

### Candidate use as-is after reached-path probe

- netstandard DNN abstractions/DI/instrumentation leaves;
- AngleSharp, BouncyCastle, MailKit/MimeKit, Newtonsoft.Json, SharpZipLib,
  PetaPoco, and Microsoft.Extensions DI;
- SQL client libraries on a TCP SQL Server connection; and
- static JavaScript, CSS, fonts, icons, templates, localization files, and
  manifest data.

“Modern package exists” is not proof. Lucene.Net.Contrib 3.0.3, QuickIO.NET,
Microsoft.ApplicationBlocks.Data, ClientDependency, WebFormsMvp, and DNN's
vendored log4net require direct probes or source rebuild decisions.

### Substitute or isolate

| Dependency/behavior | Direction |
| --- | --- |
| `System.Data.SqlClient` on net48 | Prefer `Microsoft.Data.SqlClient` only if source migration is required; preserve SQL semantics and schema scripts |
| Framework CodeDOM provider | Use Rehost's Roslyn page compiler; remove/bypass `Microsoft.CodeDom.Providers.DotNetCompilerPlatform` configuration only after generated-page evidence |
| Windows/IIS install verification | Replace with host-owned capability validation; current code requires integrated pipeline and reads the .NET 4.8 release from HKLM ([gate](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Library/Services/Upgrade/Internals/Steps/IISVerificationStep.cs#L25-L52)) |
| App-domain recycle after config/bin changes | Publish a new immutable application generation and replace the process; never emulate in-process AppDomain replacement |
| `System.Drawing` image generation/manipulation | Inventory the reached upload/thumbnail/captcha paths, then substitute a portable image library or mark specific features unsupported |
| File-system watcher/cache invalidation | Prefer explicit generation/restart ownership; do not infer portable correctness from OS checks |

### Unsupported for the first contract

- Windows authentication/impersonation and Windows principal translation;
- remote IIS configuration and Microsoft.Web.Administration;
- registry, COM/COM+, DPAPI, WMI/System.Management, Event Log appenders, and
  Windows-only log4net impersonation/appenders;
- SQL Server Compact/attached MDF/LocalDB-style connection defaults;
- `System.Web.DynamicData`, `System.Web.Entity`, and `System.Data.Linq`-dependent
  UI paths;
- runtime VB page compilation;
- arbitrary precompiled marketplace modules whose only artifact binds
  Microsoft's `System.Web`; and
- MVC 5, Web API WebHost, WebPages/Razor and SPA modules until separately scoped.

Some Windows code is optional. For example, the core membership module checks
for a `WindowsPrincipal`, but the proposed journey uses forms authentication
([membership branch](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/HttpModules/Membership/MembershipModule.cs#L122-L153)). Conversely, the installer registry gate is on the
fresh-install critical path and cannot be dismissed as a reference-only hit.

### Unassessed closure

- ClientDependency bundling/minification and its handler/cache behavior;
- DNN's HTML editor provider and upload/browser integration;
- Lucene indexing, lock files, analyzers, and scheduler interaction;
- custom cache, folder, logging, cryptography, mail, and data providers;
- JWT/API-token authentication and antiforgery integration;
- generated image, CAPTCHA, SVG/font handling, and resx payload breadth;
- Web API route registration and message-handler authentication;
- MVC/Razor module pipeline and System.Web.WebPages configuration; and
- upgrade compatibility from prior DNN versions rather than a fresh v10.3.3
  schema.

## Boot and first-request trace

This trace is inferred from source; it has not executed on Rehost.

1. Rehost validates and publishes application configuration and discovers
   application modules/handlers.
2. `Global.asax` inherits `DotNetNukeHttpApplication`
   ([source](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Website/Global.asax#L1)). Its initialization installs DNN providers and DI, then assigns
   `HttpRuntime.WebObjectActivator`
   ([source](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/DotNetNuke.Web/Common/DotNetNukeHttpApplication.cs#L60-L89)).
3. `Application_Start` chooses a server name, initializes file-change settings,
   and registers an assembly redirect
   ([source](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/DotNetNuke.Web/Common/DotNetNukeHttpApplication.cs#L181-L195)). DNS-derived server identity is ambient and must be tested for deterministic single-node behavior.
4. The configured managed modules run in authored order: request scope, request
   filter, URL rewrite, localization, mobile redirect, exception, membership,
   personalization, analytics, services, routing, ClientDependency, output
   cache ([config](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Website/release.config#L83-L115)).
5. The URL rewriter resolves host/path/query against portal aliases, maps to a
   portal/tab, and publishes `PortalSettings` into request items
   ([resolution](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/HttpModules/UrlRewrite/BasicUrlRewriter.cs#L117-L225),
   [publication](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/HttpModules/UrlRewrite/BasicUrlRewriter.cs#L259-L271)).
6. DNN validates a forms-auth cookie against persisted auth-cookie state, then
   lazily performs `Initialize.Init` and invokes request-mode scheduling from
   `BeginRequest`
   ([source](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/DotNetNuke.Web/Common/DotNetNukeHttpApplication.cs#L213-L240)).
7. Handler mapping reaches an authored DNN handler or `Default.aspx`; the page
   builds the selected portal skin/panes, loads controls for configured modules,
   applies permissions, registers client resources, renders view state and
   module output, then persistence/cache/logging modules finish the request.

The likely first blocker is therefore activation or an early module, not an
individual `.aspx` control. A “default page returned HTML” probe without module
order, portal identity, authentication principal, resources, and database
evidence would be a false success.

## Installer, schema, and upgrades

DNN is SQL Server-based in the assessed configuration. The shipped release
config defaults to SQL Express with integrated security and an attached MDF
([config](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Website/release.config#L37-L43)); that default is Windows-specific. The portable journey should use an explicit TCP SQL Server connection, SQL credentials, and a disposable database.

The installation surface is real application code: `Install.aspx`,
`InstallWizard.aspx`, and `UpgradeWizard.aspx`; DNN exempts those paths from
normal auth-cookie processing
([source](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/DotNetNuke.Web/Common/DotNetNukeHttpApplication.cs#L115-L125)). The installer:

- parses SQL connection properties, including integrated security and attached
  database forms;
- writes superuser, portal, and host choices into its install template
  ([controller](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Library/Services/Upgrade/Internals/InstallControllerImpl.cs#L45-L164));
- discovers ordered versioned `.SqlDataProvider` scripts;
- runs base schema/data scripts, version-specific managed upgrades, configuration
  transforms, cleanup, and general upgrades
  ([step](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Library/Services/Upgrade/Internals/Steps/InstallDatabaseStep.cs#L76-L172)); and
- writes `web.config`, status/install files, and application content, then
  expects application restart/reload behavior.

This conflicts with Rehost's immutable configuration generation and
process-replacement lifecycle. Rehost supports static application configuration
but configuration file watching is only Partial and requires explicit process
replacement ([map](../compatibility.md#configuration-and-iis-derived-behavior)). DNN installation
must therefore be one of:

1. **Preferred initial architecture:** offline deployment step produces a
   complete database, transformed config, binaries, and content; runtime mounts
   that immutable generation.
2. **Later compatibility mode:** installer writes a staging generation, host
   validates it atomically, then supervisor replaces the process. It must never
   expose a half-mutated live tree.

The first option changes deployment procedure, not observable post-install DNN
behavior. The second is a material host architecture feature and requires
approval before implementation.

## Modules, handlers, resources, and extension installation

DNN config depends heavily on `system.webServer/modules` and `/handlers`, not
only classic `<httpModules>`. Rehost now supports merged managed lists,
conditions, lazy handlers, and native bridges, but some global/pre-send behavior
and native authorization remain bounded
([map](../compatibility.md#configuration-and-iis-derived-behavior)).
The DNN list is therefore plausible, not proven.

Critical first-journey components:

- DNN request-scope, rewrite, membership, services, ClientDependency and output
  caching modules;
- `Default.aspx`, extensionless routing, `DependencyHandler.axd`, image/CAPTCHA,
  link, RSS, sitemap and logout handlers as reached;
- Web Forms user-control loading for module definitions stored in SQL;
- skins/containers and client assets under `Portals/_default`, DesktopModules,
  admin, controls, JS, and Resources; and
- embedded `WebResource.axd`/`ScriptResource.axd` payloads from rebuilt DNN and
  third-party assemblies.

Extension installation writes assemblies to `bin`, content into the site,
manifests/config, and database state. Arbitrary installation therefore collides
with an immutable generation and unsupported Microsoft-System.Web binary
identity. The first module should be built from the pinned source and staged
ahead of startup. A later extension contract needs package validation,
source/rebuild provenance, transactional staging, and process replacement.

## Multi-portal and host/path routing

DNN's multi-portal model fits one application per process better than separate
IIS applications: portals share process, database, application root, modules,
and caches. Selection is managed code driven from each request's domain/path.

IIS normally supplies:

- accepted site bindings and TLS termination;
- scheme, Host, port, application virtual path and physical application root;
- canonical decoded/raw URL and server-variable shapes; and
- integrated module/handler ordering.

DNN then derives the domain, looks up portal aliases, recognizes child-portal
path aliases, chooses the portal/tab, and may redirect to the configured primary
alias. Rehost already supplies Host/scheme/port and all 45 server variables, but
IIS-only values have fixed single-site or empty shapes
([map](../compatibility.md#application-and-deployment-model)). This is enough for
a plausible root-mounted host-alias slice; it is not proof for child
applications, virtual directories, forwarded-host deployments, or every DNN
friendly-URL branch.

The acceptance scenario must use two distinct Host headers against the same
process and database. It must assert different portal identity/content, correct
canonical redirects, cookies, generated absolute URLs, static/client resources,
and persistence after restart. Forwarded Host must be tested separately because
Rehost trusts only loopback forwarded headers by default.

## Authentication, session, cryptography, and authorization

The release config enables forms authentication, `.DOTNETNUKE`, anonymous
identification, a literal 3DES/SHA1 machine key placeholder, and
`SqlMembershipProvider`
([config](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Website/release.config#L133-L165),
[provider](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Website/release.config#L201-L232)). Rehost supports forms cookies, literal/auto-generated machine keys, and partial roles/profiles/anonymous identity, but its real SQL-provider journey remains open
([map](../compatibility.md#state-security-and-ancillary-assemblies)).

DNN also maintains its own users, roles, permissions, password policy, portal
membership, and persisted auth-cookie records. The first journey must prove the
real DNN providers and tables, not replace them with test providers. Required
evidence:

- installer/offline seed creates a host account and portal administrator;
- successful and rejected login, auth cookie issuance, restart survival, and
  logout;
- role-gated page/module visible to an authorized user and denied anonymously;
- anonymous/session identity continuity through ordinary navigation; and
- explicit non-production machine keys generated outside the source tree.

Windows authentication is excluded. SQL session state is unimplemented in
Rehost; use InProc initially and treat process restart as session loss unless
DNN's journey proves it does not rely on cross-restart session. Scale-out is not
part of this slice.

## Lifecycle, scheduler, and mutation

DNN supports timer- and request-driven scheduling. `BeginRequest` can launch
request-mode work, while timer mode creates background threads; source comments
explicitly account for overlapping IIS recycle
([scheduler modes](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Library/Services/Scheduling/SchedulingProvider.cs#L43-L104),
[IIS recycle assumption](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Library/Services/Scheduling/SchedulingProvider.cs#L164-L176)). `Application_End` stops scheduling and disposes Lucene
([source](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/DotNetNuke.Web/Common/DotNetNukeHttpApplication.cs#L132-L179)).

Initial journey: disable scheduler. Later work must define single-node versus
multi-node ownership, shutdown deadline, duplicate-work protection, database
leases, and behavior when Rehost replaces a process. Background threads must
not be allowed to blur generation ownership.

Configuration, extension, resource, cache and `bin` mutation all require the
same decision. Rehost's safe model is a supervisor-published immutable
generation, not live tree mutation plus file-change notification.

## Cross-platform source and build hazards

Static scans establish risk, not reached failure:

- explicit HKLM registry access in the install gate;
- Windows principal/identity and impersonation paths;
- System.Management in the Google SMTP OAuth provider;
- Microsoft.Web.Administration/ServerManager references;
- vendored log4net appenders and security contexts with Kernel32, Advapi32,
  Netapi32, Event Log and Windows identity calls;
- substantial `System.Drawing` usage for image, upload, thumbnail and CAPTCHA
  behavior;
- backslash literals in runtime paths, including module source-file paths
  ([example](https://github.com/dnnsoftware/Dnn.Platform/blob/b417ffad45522ed718edb72c0a5d77f72249690e/DNN%20Platform/Library/Entities/Modules/PortalModuleBase.cs#L127-L142));
- legacy project `HintPath`, content links and MSBuild imports using backslashes;
- two VB assemblies and legacy Framework references absent from modern .NET;
- case-sensitive lookup risk across thousands of authored content/resource
  paths; and
- npm/yarn/Cake/custom package targets whose generated assets and install ZIP
  layout may be load-bearing.

The app source, including any runtime-owned App_Code/CodeFile sources introduced
by modules, needs a complete separator and case audit. OS checks are insufficient:
case-sensitive paths must run on the repository's disposable case-sensitive
macOS fixture and Linux. Do not edit the frozen upstream tree casually; if an
otherwise portable critical path contains path-combine defects, record explicit
minimal exceptions, as the AJAX sample precedent did.

Build output is part of architecture. Before import, compare an official
v10.3.3 install package against source/build declarations: binaries, manifests,
SQL scripts, generated/minified assets, skins, providers, config transforms,
and file permissions. Do not assume the Git checkout alone is a deployable
website.

## Gap comparison with current Rehost

| DNN need | Current evidence | Assessment |
| --- | --- | --- |
| net48 WAP staging | WAP supported through App/Host sidecar; publish policy remains open | Plausible, but unprecedented project count/payload |
| Managed IIS modules/handlers | Supported with stated global/pre-send/native boundaries | Core DNN path must be traced event by event |
| Host/scheme/server variables | Supported, IIS-only variables simplified | Root host-alias slice plausible; child apps/proxy behavior unproven |
| Forms auth/membership/roles/profile | Partial; fake providers and Identity journey, real SQL provider open | Critical gap |
| InProc session | Partial, meaningful commerce journey exists | Plausible for first slice |
| SQL Server application data | Wingtip proves ordinary EF/SQL app behavior, not DNN provider/install chain | Critical app-level proof |
| Web Forms pages, controls, resources | Broad supported core plus many unassessed controls | Critical breadth risk |
| App_Browsers | Supported since 2026-09-25 (application folder only) | DNN's shipped `App_Browsers` should compile as-is; unmeasured against DNN's files |
| DynamicData/Entity/Data.Linq | absent/unassessed/unsupported paths | Exclude reached modules or port separately |
| MVC/Web API/Razor | no broad support claim | Out of first scope; likely major separate workstream |
| Config reload/bin mutation | Partial; immutable generation/process replacement | Architecture decision |
| Scheduler/background work | No DNN-specific support evidence | Disable first; design later |
| Drawing | platform-limited resx/drawing boundary | Likely substitute for reached image paths |
| Native IIS/Windows administration | Unsupported | Explicitly exclude/bypass |

## Recommended first journey

Use a disposable v10.3.3 deployment payload and database. Preserve a frozen
upstream source tree; build a DNN `.App` closure from project/package references,
stage content separately, and use a small `.Host`. XDT changes belong in a
staged `Web.Rehost.config`, never upstream `release.config`.

### Milestone 0 — reproducible payload and closure

- Obtain the official v10.3.3 install artifact and record checksum/provenance.
- Diff it against source build outputs; enumerate all required projects,
  binaries, generated assets, config transforms and SQL scripts.
- Produce a dependency ledger with `use as-is`, `recompile`, `substitute`,
  `unsupported`, and `unassessed`; attach reached-path evidence to every binary
  consumed as-is.
- Pre-create a v10.3.3 database and site config offline. Disable scheduler,
  outbound mail, search indexing, analytics, mobile redirect, optional auth,
  auto-upgrade, and extension installation unless startup requires them.

Exit evidence: deterministic staged tree/database from a clean checkout; no
build or runtime writes into source; compile failures categorized rather than
suppressed.

### Milestone 1 — activation and anonymous first request

- Start one root-mounted application and issue `GET /` with an explicit Host.
- Trace every DNN and Rehost pipeline event, handler choice, portal alias,
  database query boundary, status/redirect, cookie, and resource URL.
- Render the default portal skin and a built-in Web Forms HTML module with all
  required CSS/JS/images returning correct content types.

Exit evidence: repeatable cold/warm wire transcript and body markers on macOS,
Linux, and Windows; failures attributable to reached code.

### Milestone 2 — administration and content mutation

- Log in as host/admin through DNN's real provider.
- Create a page, add a built-in Web Forms HTML module, edit and save content.
- Verify anonymous visibility and role-restricted denial/allow behavior.
- Restart the process and prove page/content/user/role persistence and valid
  authentication-key behavior; session persistence is not required.

Exit evidence: SQL writes identified, forms ticket and authorization behavior
captured, rendered postbacks use actual generated actions/view state.

### Milestone 3 — two host aliases, one process

- Configure two portal aliases (prefer two portals if manageable; otherwise two
  aliases with a canonical redirect) against the same process/database.
- Request both via distinct Host headers and verify portal selection, absolute
  URLs, redirects, cookies, resources, and authorization isolation.
- Repeat after process restart and once behind the supported forwarded-header
  configuration.

Exit evidence: managed routing, not external reverse-proxy rewriting, proves
alias behavior.

### Milestone 4 — fresh install decision

Only after Milestones 1–3, choose whether to support:

- an offline DNN deployment command that owns DB/config/content generation; or
- the browser installer writing a staging generation with supervisor-mediated
  atomic publication/restart.

Do not adapt the current installer directly to mutate the live generation.

## Effort and uncertainty

No calendar estimate is defensible before the compile and activation probes.
Relative effort:

| Workstream | Expected effort | Uncertainty driver |
| --- | --- | --- |
| Source/project closure and SDK conversion | Very high | 60+ System.Web-bound projects, VB projects, cycles, legacy targets |
| Managed pipeline and first request | High | many always-on modules; one early incompatibility blocks all pages |
| Web Forms UI/control breadth | High | DNN custom controls, skins, dynamic loading, resources |
| Dependency probing/recompilation | Very high | old packages plus binary identity; reached paths unknown |
| SQL data provider and seeded deployment | Medium-high | core SQL likely portable; install/upgrade ordering is large |
| Fresh installer/upgrade lifecycle | Very high | registry gate, config/tree mutation, restart, rollback |
| Multi-portal aliases | Medium after boot | managed design fits; URL/server-variable edge breadth |
| Auth/roles/crypto | High | real DNN providers absent from current Rehost evidence |
| Assets/build/packaging | High | generated install payload and front-end toolchain |
| Cross-platform cleanup | High | case/separators/drawing/Windows APIs across a large tree |
| Scheduler/search/background work | Medium-high, deferrable | IIS recycle assumptions, threads, Lucene locks, multi-node ownership |
| MVC/Web API/Razor/SPA breadth | Very high, excluded | no current application-level Rehost proof |

The strongest uncertainty-reducing probes are, in order: deterministic official
payload diff, compile-error inventory for the minimal WAP closure, activation
through all configured modules against a preinstalled DB, then one anonymous
page/resource round trip. Fresh installer work before those probes would spend
heavily without answering feasibility.

## Decisions requiring approval

1. Preinstalled immutable database/config generation versus browser installer.
2. Web Forms-only first compatibility boundary versus including Persona Bar's
   API/MVC/SPA dependencies.
3. Source-rebuild policy for built-in and third-party DNN assemblies.
4. Portable substitute policy for image/CAPTCHA/thumbnail features.
5. Extension installation contract: prebuilt curated modules only, source
   rebuild pipeline, or arbitrary binary packages (the last conflicts with the
   project's binary-identity contract).
6. Scheduler disabled initially versus host-owned durable scheduling.
7. Root-mounted one-app-per-process only versus child application/virtual path
   support.

## Unknowns to resolve with evidence

- Exact minimal project and module closure for the chosen admin/create-page
  journey.
- Whether Persona Bar makes Web API/MVC/Razor unavoidable for page creation in
  v10.3.3; if so, choose an older Web Forms admin route or widen scope explicitly.
- First compile failures against current Rehost packages, including missing
  System.Web API shape and VB assembly dependencies.
- Which configured DNN modules reach unsupported integrated/native behavior.
- Real ClientDependency handler/resource/cache behavior on Rehost.
- DNN SQL provider compatibility with the selected modern SQL client and SQL
  Server container/host.
- Whether DNN's shipped `App_Browsers` compiles and identifies browsers as on
  Framework.
- Reached DynamicData/Entity/Data.Linq types in the bounded module.
- Required System.Drawing paths in installation, login, page edit, and module
  rendering.
- Case and separator defects in content, resources, manifests, skins and module
  source paths.
- Crypto compatibility of existing DNN password hashes, forms tickets, API
  tokens, and persisted auth-cookie rows.
- Behavior after graceful stop versus crash while DNN background work or cache
  persistence is active.
- Official install artifact versus repository-generated payload differences.
- License/provenance obligations for copied/recompiled DNN and vendored code.

Until these are answered, the correct claim is **static feasibility with known
architecture decisions**, not “DNN runs on Rehost.”
