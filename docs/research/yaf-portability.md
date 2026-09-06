# YAF.NET 3.2 portability assessment

Everything below the horizontal rule is the original static assessment, kept as
written so its predictions can be scored. The bring-up has since run: see
[the app notes](../../apps/YAF/README.md) and
[the provenance record](../provenance/yafnet.md). Neither this document nor those
widen [`compatibility.md`](../compatibility.md).

## What the bring-up measured

The 14-project SQL Server closure compiles and the forum runs: anonymous read,
registration with the verification mail, sign-in, a topic, a member's reply, a
moderation delete, and the permission differences between guest, member and
administrator. Fifty-four scripted checks pass on macOS arm64 and Linux.

### Predictions that held

- Web API's `System.Web.Http.WebHost` is the one Framework-bound assembly on the
  path, and `Application_Start` reaches it unconditionally.
- All reached YAF assemblies bind Framework's `System.Web` and need rebuilding.
- `Microsoft.Owin.Host.SystemWeb` substitutes cleanly.
- `Session_Start` is where initialization happens, and the database check
  redirects a fresh board to the installer.
- The installer's config mutation conflicts with immutable configuration; the
  install is a deployment step here, scripted as `install.sh`.
- Case-sensitivity defects exist. One was measured, and only Linux showed it:
  `web.config` names `URLRewriter.config`, the file is `UrlRewriter.config`.

### Predictions that were wrong

- **Web API was called the likely first blocker and a possible new workstream.**
  It is a 3,073-line recompile from `aspnet/AspNetWebStack` v3.3.0 with one
  field's `readonly` dropped, and Web API Core needs nothing at all: it
  references no `System.Web`. No new Rehost seam was required.
- **`System.Data.Linq` was called unsupported if reached.** It is reached, by one
  attribute on one obsolete model, and a five-line shim covers it.
- **`System.Web.DynamicData` and `System.Web.Entity` were called compile
  blockers.** The frozen projects reference them and nothing in the closure
  reaches them; dropping the references costs nothing.
- **Lucene was called a recompile-or-defer decision.** All five projects compile
  unchanged. Their build file only knows target frameworks up to net9, so net10
  gets none of its `FEATURE_*` defines, which is the same set net481 got.

### Blockers the static pass did not predict

- **C# 14's first-class spans.** `array.Contains(x)` rebinds from `Enumerable` to
  `MemoryExtensions`, and OrmLite cannot box the resulting `ReadOnlySpan` into an
  expression tree. Pinning `LangVersion` 13 restores the net481 reading. Any
  frozen tree rebuilt on .NET 10 has this exposure.
- **`<system.net>` fails activation outright**, and mail cannot be configured from
  `web.config` on modern .NET at all
  ([reading](../follow-ups/system-net-mail-settings.md)). Every YAF
  user-creation path sends a verification mail, so this gates registration.
- **An upstream YAF defect.** Since v3.2.14, `Migration01` returns before creating
  the five ASP.NET Identity tables on every provider except MySQL, so a fresh
  SQL Server install cannot create a board.
- **Framework reflection permissiveness.** YAF sets a static `initonly` field by
  reflection during `Application_Start`; .NET refuses where Framework allowed it.
- **`App_Browsers` fails activation**, which the map already records as
  Unsupported. Excluding it leaves the rendered markup uplevel.

### Still unmeasured

Search and indexing, image and avatar paths, attachments, private messages,
multi-board creation, the ten Web API controllers, virtual-directory hosting, the
upgrade path, and mail over a network. The Framework/IIS baseline the original
plan called for was never taken: the port was measured against YAF's own expected
behavior and its source, not against a running Framework instance.

---


## Executive verdict

**YAF.NET 3.2 is a materially smaller next application than DNN and materially
richer than Wingtip Toys.** Its SQL Server Web Forms path has a bounded 14-project
source closure, one principal page/control application, one rewrite module, three
custom handlers, OWIN cookie identity, Web API endpoints, database migrations,
real forum permissions, uploads, search, and moderation. DNN's multi-portal,
extension, scheduler, Persona Bar, and 60-plus-project breadth are absent.

It is not ready to call portable. The first likely compile/activation blockers are
`System.Web.Http.WebHost`, `System.Web.DynamicData`/`System.Web.Entity`, and
YAF's first-session startup chain. The first two may be removable from the bounded
journey, but that is static inference. The hard known boundary is binary identity:
all reached YAF assemblies bind Microsoft's `System.Web` and must be rebuilt.
Uploads that resize images reach `System.Drawing`; multi-board creation contains
literal backslash path segments. Live installer/config mutation conflicts with
Rehost's immutable generation model.

Recommended first proof: a preinstalled SQL Server database, frozen YAF tree,
package-based `.App`/`.Host` sidecars, staged XDT config, search disabled, database
attachments disabled, and one journey: register, create topic, reply, moderate,
then prove anonymous/member/moderator permission differences and persistence after
process restart.

## Source pin and method

Assessed upstream: official `YAFNET/YAFNET` tag `v3.2.16`, commit
`339f1c15cad71bfcca7d12b44a9ead48563f410e`, commit timestamp
`2026-08-29T12:29:13+02:00`. Tag and SHA were verified from the official clone on
`2026-09-05T19:24:56Z`. The tag is lightweight. `v3.2.16` was the highest stable
3.2.x tag then present; its website targets .NET Framework 4.8.1
([project](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YetAnotherForum.NET/YAF-SqlServer.csproj#L23-L35)).
Current `master` was rejected because it is not the requested legacy line.

Evidence is source reachability, project/config declarations, and comparison with
Rehost's current map. “Plausible” and “likely” are inference, never measured proof.
Third-party package compatibility cannot be inferred from target framework or a
successful reference alone; reached paths need probes, following the application
bring-up method ([guide](../bringing-up-an-application.md)).

## Topology and reached project closure

The repository contains 23 production `.csproj` files and four database-specific
website projects. Do not equate every project, or every `System.Web` reference,
with the first journey. For `YAF-SqlServer.csproj`, the statically reached build
closure is 14 projects:

| Layer | Reached projects | Why |
| --- | --- | --- |
| Website | `YetAnotherForum.NET/YAF-SqlServer` | WAP, pages, controls, content |
| YAF | `YAF.Configuration`, `YAF.Types`, `YAF.Core`, `YAF.UrlRewriter`, `YAF.Web`, `YAF.Data.SqlServer` | Direct website references |
| Data | vendored `ServiceStack.OrmLite`, `ServiceStack.OrmLite.SqlServer` | Types/core/data references |
| Search | vendored Lucene base, Analysis.Common, Highlighter, Queries, QueryParser | Direct `YAF.Core` references |

The website's seven direct project references are visible in its project file
([references](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YetAnotherForum.NET/YAF-SqlServer.csproj#L2934-L2961)); `YAF.Core` adds five Lucene projects plus OrmLite, Configuration, and Types
([core project](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/YAF.Core.csproj#L73-L85)).
The SQL data project adds only the SQL Server OrmLite adapter beyond that closure
([data project](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Data/YAF.Data.SqlServer/YAF.Data.SqlServer.csproj#L33-L46)).

This is a WAP, not a Web Site: code-behind and controls compile into the app
assembly. No `App_Code` directory exists at the pin. Therefore the normal frozen
legacy tree plus `.App` and `.Host` sidecars is the default. A Host-only Web Site
source-staging workaround, required by the AJAX sample, is not indicated. Runtime
compilation still covers markup/`Global.asax`; the global file inherits the
compiled `YafHttpApplication` ([Global.asax](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YetAnotherForum.NET/Global.asax#L1)).

## Dependency disposition

These are initial dispositions, not probe results.

| Dependency/assembly | Disposition | Evidence and boundary |
| --- | --- | --- |
| YAF website + six YAF libraries | **Recompile** | All are source projects in the reached closure; Configuration, Types, Core, UrlRewriter, and Web directly reference Framework `System.Web`. Binary interchangeability is unsupported. |
| `Microsoft.Owin.Host.SystemWeb` 4.2.3 | **Substitute** | Use `Rehost.WebForms.Owin.Host.SystemWeb`; YAF directly references the Framework host ([core project](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/YAF.Core.csproj#L36-L44)). Katana startup/cookie auth is already partially supported by Rehost. |
| `Microsoft.Owin`, Security, Cookies, OAuth | **Use as-is, probe reached path** | Existing Identity app consumes these package families, but YAF's exact cookie validation and data-protection path remains unmeasured. |
| Microsoft ASP.NET Identity Core/Owin 2.2.4 | **Use as-is, probe reached path** | Same family works in the template app; YAF supplies its own managers/stores and therefore needs its actual register/login path driven. |
| ASP.NET Web API Core/WebHost 5.3.0 | **Unassessed; likely blocker** | `Application_Start` always calls `GlobalConfiguration.Configure`; registration reflects into `HttpControllerRouteHandler._instance` then maps attribute routes ([application](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Context/YAFHttpApplication.cs#L145-L149), [Web API config](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Context/Start/WebApiConfig.cs#L49-L64)). Rehost makes no broad Web API hosting claim. Recompile WebHost or introduce a seam only after compile/reached evidence. |
| Autofac, Newtonsoft.Json, OEmbed.Core, Farsi.Library | **Use as-is, probe** | Package references; no evidence yet that their reached paths bind Framework-only identities. Autofac is activation-critical. |
| vendored ServiceStack.OrmLite + SQL Server adapter | **Recompile** | In source closure and used by repositories/migrations. Its source is not `System.Web`-bound, but rebuilding preserves one coherent source provenance and modern SQL-client substitution may be required. |
| vendored Lucene five-project subset | **Recompile or defer** | Statically direct from Core, but search is optional to the first journey. Source closure does not prove runtime necessity. Prefer compiling it unchanged first; disable indexing/background search during activation. |
| `System.Runtime.Caching`, BCL support packages | **Use/substitute after compile inventory** | Modern implementations exist; exact API and assembly binding need compile evidence. |
| `System.Data.Linq` | **Unsupported if reached** | Types and website reference it; Rehost explicitly excludes LinqDataSource because modern `System.Data.Linq` is absent. Search usages and first-journey reachability before deciding whether to excise a reference. |
| `System.Web.DynamicData`, `System.Web.Entity` | **Unsupported/unassessed if reached** | Website references both; Rehost carries no DynamicData assembly and calls Entity unassessed. Mere references do not prove need. Compile diagnostics and source/type reachability decide. |
| `System.ServiceModel`, EnterpriseServices | **Unsupported/unassessed if reached** | Declared by source projects; forum journey should exclude service/transaction paths unless activation reaches them. |
| `System.Drawing` | **Substitute for reached image paths** | Upload resizing calls `Image.FromStream` and image save ([uploader](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Handlers/FileUploader.cs#L202-L215)). Plain non-image/database upload may avoid it; avatars/image attachments require a portable image library decision. |
| SQLite native interop | **Unsupported for initial slice** | SQL Server variant avoids SQLite's x64 `SQLite.Interop.dll` post-build copy. |

## Startup and first request

The sequence inferred from source/config is:

1. Rehost validates and publishes staged config, discovers pre-application OWIN
   startup, and enters the classic managed pipeline.
2. Katana invokes `Startup.Configuration`. YAF scans every loaded assembly for a
   class named `StartupSignalR`, invokes it if found, configures application cookie
   auth, captures Katana data protection, and adds the external cookie
   ([startup](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Context/Start/Startup.cs#L44-L58), [auth](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Context/Start/Startup.Auth.cs#L57-L82)). This ambient assembly scan needs deterministic closure verification; SignalR is optional unless an assembly supplies the named class.
3. `Application_Start` installs Web API attribute routes and replaces a private
   WebHost singleton through reflection. This is the likely first runtime blocker.
4. The configured `UrlRewriter` module sees managed requests. Rehost supports
   merged managed module registration, but YAF rewrite behavior is unmeasured.
5. On the first new session, `Session_Start` runs critical then noncritical
   startup services, publishes application state into the Autofac container, and
   raises YAF's init event ([application](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Context/YAFHttpApplication.cs#L160-L170), [ordering](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Extensions/IHaveServiceLocatorExtensions.cs#L41-L52)).
6. Critical database startup redirects missing/new databases to the installer,
   tests connectivity, validates schema version, and can auto-upgrade in-request
   ([database startup](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Services/Startup/StartupInitializeDb.cs#L76-L118)).
7. Rewrite/handler mapping selects `default.aspx`, `Resource.ashx`, sitemap,
   uploader, or Web API handler. The page dynamically composes YAF controls,
   resolves board/user/permissions through Autofac and SQL, then emits postback,
   resources, and content.

Important risk: initialization is tied to `Session_Start`, not solely application
activation. The initial proof must record session-cookie creation, cold request
ordering, concurrent first requests, redirect behavior, and whether a request with
session disabled can reach YAF before initialization.

## Installation, database, and deployment mutation

YAF provides SQL Server, MySQL, PostgreSQL, and SQLite variants. Choose SQL Server
first: Wingtip already proves basic SQL Server application access, and it avoids
SQLite native interop. Use explicit TCP credentials, not the recommended config's
Windows integrated-security default
([config](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YetAnotherForum.NET/recommended.web.config#L7-L28)).

Schema is code-first within YAF's own migration framework. `InstallService` runs
`Migrator` from `Migration01`, creates views/indexed views, and records version
metadata, then initializes board/admin/settings and imports static BBCode/spam data
([install](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Services/InstallService.cs#L124-L168), [migration](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Services/InstallService.cs#L184-L215)). Static import files are mapped from site content, so staging must retain the `install/` payload.

The browser installer checks application/upload directory writability. Config
helpers mutate app settings and connection strings and save `web.config`
([config writer](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Helpers/ConfigHelper.cs#L103-L175)). Auto-upgrade runs during a request. Both conflict with immutable post-publication config and process-replacement restart semantics.

Initial architecture: offline command or one-shot deployment stage creates the
database and complete XDT-produced config before host activation. Do not let YAF
mutate the frozen source or live staged generation. A later browser installer must
write a new generation, validate it, and request supervisor replacement; that is
an architecture decision, not a portability patch.

## Authentication, session, crypto, and permissions

YAF 3.2's default path is ASP.NET Identity over OWIN cookie middleware, not only
the legacy `YafMembershipProvider`. The application cookie uses a 30-minute
security-stamp validator; Katana's data-protection provider is stored for YAF
identity services ([auth](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Context/Start/Startup.Auth.cs#L64-L81)). Rehost's Identity template and Wingtip journeys make this plausible, but do not prove YAF stores, claims, security stamps, reset tokens, or role translation.

YAF also retains a `SqlMembershipProvider` subclass and clear/hashed/encrypted
legacy password handling. Treat migration of legacy users as a later compatibility
slice. Encrypted membership may reach unsupported legacy machine-key behavior.
Fresh-install Identity users avoid that path.

Session is critical, not incidental: startup runs at `Session_Start`; YAF stores
read tracking, multi-quote IDs, timestamps, and UI state in
`HttpSessionStateBase`. Rehost's InProc session is sufficient only as a plausible
single-process starting point. Restart may lose session; durable forum/user/topic
data must survive in SQL. SQL session and scale-out are excluded.

The recommended config leaves `<machineKey>` commented and tells operators to
generate one for farms ([config](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YetAnotherForum.NET/recommended.web.config#L281-L287)). Use an explicit generated test key in staged config for deterministic cookie/token restart tests. Never commit a production key.

Permission proof must use YAF's real access masks and group memberships. Assert:
anonymous cannot post; member can create/reply but not moderate; moderator can
approve/move/delete as configured; hidden/unapproved content visibility differs by
principal. A login-only smoke would not exercise the forum's defining contract.

## Modules, handlers, routing, and IIS assumptions

Recommended config registers only `UrlRewriter` as a managed module and maps
`Resource.ashx`, `Sitemap.xml`, and `FileUploader.ashx` as managed handlers
([config](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YetAnotherForum.NET/recommended.web.config#L258-L263), [integrated rows](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YetAnotherForum.NET/recommended.web.config#L344-L365)). These fit Rehost's supported managed module/handler surface. `preCondition="managedHandler"` and integrated handler conditions still require actual event/order evidence.

URL rewriting is a custom YAF module over `default.aspx`, not IIS URL Rewrite.
This is favorable. Prove raw/cooked URL, form action, canonical links, percent
encoding, path base, direct `.aspx`, friendly topic/forum routes, authorization,
and child requests. Start root-mounted; virtual-directory/AppRoot behavior is a
separate slice.

Web API is less favorable: YAF relies on `System.Web.Http.WebHost`, attribute
routing, session-enabled controller handlers, and reflection into a private static
field. No current Rehost support claim covers this. Inventory controllers reached
by ordinary topic/reply/moderation pages. If none are reached, disable registration
only through an approved narrow seam; otherwise Web API hosting becomes a real
new workstream.

Static-content MIME mappings are under `system.webServer/staticContent`, while
Rehost says unhonored server sections are unassessed. Ensure `.webp`, `.woff`,
`.woff2`, `.svg`, and `.webmanifest` are served by host mappings with expected
content types. Native IIS authorization remains irrelevant only if YAF relies on
managed permission checks for protected dynamic resources.

## Assets, packaging, uploads, and background work

The source checkout is not itself a proven deployment payload. The WAP's
`AfterBuild` depends on resource/package targets; SQL Server packaging copies many
content types and binaries, downloads/updates a global language tool, minifies
languages, creates install/upgrade ZIPs, moves files, then deletes its temporary
package tree
([packaging](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YetAnotherForum.NET/BuildScripts/YAF-SqlServer.Package.targets#L145-L220)). The WAP also runs Windows `del` commands after build
([project](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YetAnotherForum.NET/YAF-SqlServer.csproj#L2998-L3009)).

Before import, compare the official v3.2.16 SQL Server install ZIP with declared
content and clean source outputs: compiled/minified JS/CSS, language JSON,
themes, fonts, images, install data, configs, SQL/migration inputs, and DLLs.
Replace global-tool/network build behavior with pinned deterministic packaging.
App/Host package targets should stage the frozen WAP content; XDT owns environment
config. Never execute upstream post-build packaging against the source checkout.

Attachments can store bytes in SQL or on disk. Disk mode creates upload folders
and writes `.yafupload` files; resizing decodes/encodes with `System.Drawing`
([uploader](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YAF.Core/Handlers/FileUploader.cs#L221-L290)). Initial journey need not attach images. Later choose database versus an explicitly owned mutable volume, validate names, quotas, atomic writes, backup, and portable image processing.

Lucene search uses `AppDomain` data directory and filesystem locks. Treat indexing
and search as disabled/unassessed initially; later define index ownership,
rebuildability, shutdown behavior, and whether it belongs outside the immutable
generation. No broad YAF scheduler was found in the reached source, but mail,
search, cache, and asynchronous notification paths still need a background-work
inventory before enabling them.

## Cross-platform audit

A static all-`yafsrc` scan found no `App_Code` tree. It did find runtime filesystem
mutations across uploads, albums, avatars, search, upgrades, languages, and board
administration. The clearest reached defect is multi-board directory creation:
four `Path.Combine` arguments contain literal `Images\...` segments
([source](https://github.com/YAFNET/YAFNET/blob/339f1c15cad71bfcca7d12b44a9ead48563f410e/yafsrc/YetAnotherForum.NET/Pages/Admin/EditBoard.ascx.cs#L153-L171)). On Unix those are names containing backslashes. Exclude multi-board creation initially; a future minimal frozen-source exception needs explicit approval and a Linux/case-sensitive regression test.

Other risks:

- case-sensitive content lookup across themes, languages, resources, icons, and
  markup paths;
- Windows separators throughout legacy project/package files and signing-key paths;
- SQLite's native x64 interop copy;
- `System.Drawing` in uploads/avatar editing;
- file moves during upgrades and mutable language/admin features;
- assembly scanning from `AppDomain.BaseDirectory`/`RelativeSearchPath`, including
  `Assembly.LoadFrom`, whose results depend on staged `bin` contents.

Run a complete authored path-reference case audit against the actual install
payload, not only C# strings. Exercise case behavior on the disposable
case-sensitive macOS filesystem and Linux; OS checks are insufficient.

## Gap comparison with Rehost

| YAF need | Current Rehost evidence | Assessment |
| --- | --- | --- |
| net481 WAP + package sidecars | Supported by three frozen WAP applications | Strong fit; 14-project closure still uncompiled |
| Web Forms controls/postbacks/view state | Broad support, Wingtip stateful journey | Plausible; YAF control breadth unmeasured |
| Katana SystemWeb + cookie Identity | Partial; Identity and Wingtip journeys | Plausible, real YAF store/token proof required |
| InProc session | Partial, meaningful commerce journey | Critical due to `Session_Start` initialization |
| Managed module/handlers | Supported with boundaries | Small configured set; rewrite semantics unproven |
| Web API WebHost/attribute routes | No broad support claim | Likely first blocker or removable optional path |
| SQL Server persistence | Wingtip proves ordinary SQL use | YAF OrmLite/migrations/access masks unproven |
| `System.Web.DynamicData`/Entity/Data.Linq | absent/unassessed/unsupported boundaries | Compile/reachability blockers; likely removable refs must be proven |
| Resource/static assets | Static files and WebResource supported | Packaging and MIME types unproven |
| File upload/save | Request upload and SaveAs supported | YAF disk ownership + drawing are application gaps |
| `System.Drawing` | Platform-limited | Substitute when image paths enter scope |
| Live config/reload | Immutable config; reload Partial | Browser installer/auto-upgrade architecture conflict |
| Search/index files | No YAF-specific evidence | Disable first; design durable ownership later |

## Recommended bounded journey and stages

### Stage 0 — payload, compile, and database seed

- Pin official install artifact checksum; diff it against source/package targets.
- Build only the 14-project SQL Server closure in sidecars; categorize compile
  errors by reached necessity. Do not import all 23 projects reflexively.
- Produce an exact dependency ledger and API-use inventory. Probe each third-party
  binary's reached startup and journey path.
- Create database/config offline. Explicit TCP SQL credentials and machine key;
  root mount; search, Web API if proven optional, image resizing, external auth,
  mail, auto-upgrade, multi-board, and disk attachments off.

Exit: deterministic staged generation and seeded DB; no write to frozen source;
all activation-critical compile gaps named.

### Stage 1 — cold activation and anonymous forum

- Trace OWIN, `Application_Start`, session creation/startup services, rewrite,
  handler/page selection, Autofac construction, SQL, status/redirect/cookies.
- Render forum index, topic list, topic, resources, CSS/JS/fonts/images on cold and
  warm requests. Assert friendly and direct URLs and rendered postback actions.
- Record Framework/IIS baseline first, then macOS/Linux/Windows wire parity.

Exit: anonymous read path, resources, and initialization repeatably pass.

### Stage 2 — identity, topic, reply, moderation

- Register a real YAF Identity user; reject bad login; sign in/out.
- Create topic and reply through rendered forms/view state.
- Configure a moderator and exercise an actual moderation transition.
- Assert anonymous/member/moderator permission differences in UI and server-side
  denial, including direct URL/post attempts.
- Restart process: users, roles, topic, reply, moderation state persist; auth
  cookie/token behavior is explicitly recorded. Session persistence not required.

Exit: the representative stateful forum contract works on three platforms.

### Stage 3 — optional breadth

Add one at a time: Web API endpoint, search/index rebuild, non-image attachment,
portable image/avatar processing, mail, virtual path, multi-board. Each owns its
mutable storage and reached dependency proof.

### Stage 4 — install/upgrade decision

Choose offline deployment generation or staging-generation browser install with
supervisor publication. Never adapt the installer to mutate the live generation.

## Effort and uncertainty

| Workstream | Relative effort | Main uncertainty |
| --- | --- | --- |
| 14-project compile closure | Medium-high | missing WebHost/DynamicData/Entity/Data.Linq API shape |
| Cold activation/session startup | High | Web API reflection, Autofac scanning, critical DB service order |
| SQL migration/seed | Medium | vendored OrmLite and migration dialect behavior |
| Register/topic/reply | Medium-high | control/API breadth and real Identity stores |
| Moderation/permissions | Medium | app complexity, but narrow runtime surface |
| Assets/package reconstruction | Medium-high | generated/minified payload and global tool |
| Cross-platform filesystem | Medium | case/path defects; mutable volumes |
| Drawing/uploads | Medium, deferrable | portable image substitution |
| Search | Medium, deferrable | Lucene file/lock/lifecycle ownership |
| Browser install/upgrade | High, deferrable | config mutation and atomic restart |

This is substantially below DNN's very-high closure/pipeline/extension breadth,
but above Wingtip because it adds a multi-project source rebuild, real permission
model, custom rewrite/handlers, migration engine, optional Web API, search, and
uploads. Best next probe: compile inventory, then cold activation against an
offline-seeded DB. Those two decide feasibility far more cheaply than installer or
image work.

## Decisions requiring approval

1. Keep Web API outside the first journey if source/reached tracing permits, or
   add a Web API hosting workstream.
2. Rebuild vendored Lucene now versus temporarily sever optional search references.
3. Offline-only install/upgrade versus a supervisor-published staging generation.
4. Database attachments versus host-owned mutable upload volume.
5. Portable image library and compatibility boundary for resize/avatar behavior.
6. Whether any proven path/case defect may be a documented minimal exception in
   the otherwise frozen tree.
7. Root-mounted forum only versus virtual path/embedded forum support.

## Unknowns requiring measured proof

- Exact compiler/API failures against current Rehost packages.
- Whether `Application_Start` can omit Web API registration without affecting the
  bounded journey, and which controllers pages call.
- Autofac module discovery order and the actual startup service set.
- Framework/IIS first-session ordering and concurrent cold-request behavior.
- YAF Identity schema, claims, security-stamp, token, and cookie restart behavior.
- OrmLite SQL Server provider/client compatibility and migration SQL.
- Which `System.Data.Linq`, DynamicData, Entity, WCF, or EnterpriseServices types
  are truly reached.
- Official install ZIP differences from source-declared content and generated
  language/front-end assets.
- Rewriter parity for Unicode, escaping, canonical URLs, virtual paths, and
  authorization.
- Case mismatches across every markup/config/CSS/JS/resource path.
- Search index lifecycle and shutdown safety.
- License/provenance obligations for YAF and vendored Lucene/ServiceStack source.

Until those probes pass, correct claim: **promising static candidate with a bounded
first journey and known architecture choices; YAF does not yet run on Rehost.**
