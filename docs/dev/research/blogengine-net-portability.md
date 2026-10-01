# BlogEngine.NET portability assessment

Preliminary static assessment of BlogEngine.NET on Rehost.Web. This is
not an import plan or support claim. No BlogEngine build, setup flow, or
application code was executed. Any later baseline or probe must use a
disposable copy, never the source checkout. The
[compatibility map](../compatibility.md) remains the sole source of Rehost
support claims.

## Source and assessment boundary

The official repository was cloned for research at
`/Users/vm/repos/BlogEngine.NET`. It did not exist beforehand. The checkout is
clean and detached at the latest GitHub release, **v3.3.8.0**, commit
`e81ba7425e860c2265f66911436bd241061274f9`, released 2019-06-15 as a security
patch ([release](https://github.com/BlogEngine/BlogEngine.NET/releases/tag/v3.3.8.0)).
All source links below pin that commit. Assessment date: 2026-09-05.

The default branch was `master` at
`95c84261ed94402f45094c1fda85afbbf6f4d833` when cloned, 31 commits beyond the
release. It contains later security changes, including fixes identified as
CVE-2022-41417 and CVE-2022-41418
([history](https://github.com/BlogEngine/BlogEngine.NET/compare/v3.3.8.0...95c84261ed94402f45094c1fda85afbbf6f4d833)).
It is not a tagged release. The stable tag is therefore the reproducible
compatibility target, but it is suitable only for an isolated research fixture.
A deployable port must explicitly choose a provenance-preserving security patch
overlay or a reviewed unreleased revision; silently publishing v3.3.8.0 would
be unsafe.

Scope is the default single-blog XML configuration and a bounded journey:
anonymous reading, XML-provider login, author create/edit/publish, anonymous
comment, moderation, feed/search, and restart persistence. Multi-blog, database
providers, package gallery/update, theme/widget installation, remote publishing,
and optional handlers are classified but excluded initially.

## Verdict

**Conditionally feasible; medium-high effort; substantially smaller than DNN,
but blocked by missing Web API/WebPages evidence for its real administration
path.** BlogEngine is a useful next application because it combines only two
production projects with a stateful filesystem-backed CMS, custom providers,
managed modules/handlers, Razor-hosted administration, and image/file mutation.
That adds evidence Wingtip does not: XML membership/roles, durable non-database
content, comment moderation, feeds/search, and writable application data.

There is no demonstrated irreducible blocker for anonymous Web Forms rendering.
The requested authoring journey, however, is not a Web Forms-only path:
`/admin/index.cshtml` hosts an Angular shell and the post/comment operations are
`ApiController` endpoints. Rehost currently makes no broad Web API, WebHost,
WebPages, or Razor execution claim. Those are probable critical blockers, not
optional references.

Recommended next step: a two-gate spike smaller than DNN. First compile and
activate the two-project closure, default XML provider, anonymous home/post,
assets, search, and RSS. Only if that passes, scope the minimum Web API +
WebPages/Razor surface required by login/admin/post/comment moderation. Do not
start database-provider, installer, marketplace, or live-update work first.

## Application and repository shape

The solution has three C# projects: `BlogEngine.Core`, the `BlogEngine.NET` web
application, and tests. The runtime closure begins with **two**, not every
referenced assembly: the net45 WAP directly references only `BlogEngine.Core`
([project reference](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/BlogEngine.NET.csproj#L2232-L2238)).
Both production assemblies directly bind Framework `System.Web`, so both must
be rebuilt against Rehost. Tests are not runtime dependencies. The exact
third-party rebuild set remains reached-path work; reference count does not
prove mandatory rebuild count.

| Layer | Concrete shape | Initial disposition |
| --- | --- | --- |
| Website | Old-style net45 Web Application Project with 153 compiled source entries, code-behind/designer files, inline `Global.asax`, 16 `.aspx`, 26 `.ascx`, 37 `.cshtml`, and one `.asmx` | Frozen WAP content plus package-based `.App`; small `.Host`; XDT-staged config |
| Core | Old-style net45 class library with 237 compiled source entries; providers, domain objects, modules, handlers, search, feeds, packaging | Rebuild against Rehost |
| `AppCode/` | Despite its name, ordinary WAP sources explicitly compiled into `BlogEngine.NET.dll`, not runtime `App_Code` | Compile in `.App`; do not use Web Site source staging |
| Admin | Razor/WebPages shell, Angular assets, Web API controllers, SimpleInjector Web API integration | Critical second gate; not optional for requested author journey |
| Data | Default XML content, users, roles, settings, comments, files under `App_Data` | Copy seed into writable generation-owned data area; prove persistence |
| Optional stores | SQL Server, SQL CE, SQLite, MySQL setup scripts/configs | Exclude initially; assess one provider later |
| Assets | Themes, widgets, editors, scripts, CSS, fonts, resources, images | Stage as deterministic content; preserve case exactly |
| Tooling | Legacy WebApplication targets and optional TypeScript 1.6 targets; generated designer/resource files are committed | Sidecars replace legacy build plumbing; inventory payload rather than run tooling |

This is a WAP, not a Web Site. The normal frozen source + `.App` + `.Host`
contract applies. `AppCode` is not named `App_Code`, and its files are listed in
the WAP project. Host-only runtime-code staging, needed by the AJAX Toolkit Web
Site, would be wrong here. The app and host should consume Rehost packages;
configuration differences belong in `Web.Rehost.config` and the staged XDT
pipeline, never in upstream `Web.Config`.

The project imports optional Visual Studio TypeScript targets and WebApplication
targets, but declares no active custom build target or non-empty pre/post-build
event ([project tail](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/BlogEngine.NET.csproj#L2440-L2470)).
The checked-in JavaScript/CSS/editor payload therefore appears sufficient
statically. A later deployment inventory must still compare release assets and
project content; absence of an active generator is not runtime proof.

## Dependency classification

Every classification is provisional until a net10 probe drives the exact
BlogEngine path. Wingtip's Google-provider startup failure proves that a binary
which loads or references successfully can still fail when a reached constructor
demands a missing Framework facade.

| Dependency | Likely disposition | Reason/boundary |
| --- | --- | --- |
| `BlogEngine.NET`, `BlogEngine.Core` | Recompile | Both bind Microsoft's strong-named `System.Web`; Rehost supports source/API rebuild, not binary identity |
| `System.Web.Extensions` | Rehost package | ScriptManager/page services exist, but BlogEngine's configured authentication service is unassessed |
| `System.Web.Optimization`, WebGrease, Antlr | Rehost Optimization packages | Bundle registration is unconditional on first request; production output needs direct evidence |
| Microsoft ASP.NET Web API Core/WebHost 5.2.3 | Recompile or port/substitute | WebHost binds Framework `System.Web`; admin content operations directly require `ApiController` routing/hosting |
| WebPages/Razor 3.2.3 and `System.Web.Helpers` | Recompile or port/substitute | `/admin/index.cshtml`, layouts and editors are the real admin shell; no current Rehost execution claim |
| SimpleInjector 3.1 + execution-context scoping | Candidate use as-is | Pure DI core plausible; exact execution-context path must be probed |
| SimpleInjector Web API integration | Recompile/probe | Reached during first-request container registration and coupled to Web API |
| Newtonsoft.Json | Candidate use as-is, preferably reviewed upgrade | Managed; old versions are not a compatibility requirement, but serialization differences need admin API evidence |
| Microsoft.Web.Infrastructure | Likely substitute/drop | Pre-start infrastructure may be unnecessary after rebuilding; prove no reached registration dependency |
| AjaxMin, BlogML, DynamicQuery, SharpZipLib | Unassessed | Vendored/old binaries; some are optional import/export/package paths, but static references alone decide nothing |
| NuGet.Core 2.8.2 + Microsoft.Web.Xdt | Unsupported initially | Package installation/update/config mutation is excluded from first journey |
| `System.Data.Linq`, Data Services Client, DirectoryServices, ServiceModel | Unsupported or unassessed paths | Not required merely because Core references them; database/gallery/WCF paths need reachability proof |
| `System.Drawing` | Substitute for reached image operations | Profile upload and image thumbnail/edit paths directly use GDI+ APIs |
| Static front-end/theme/editor assets | Use as-is | Content payload; verify case, URLs, MIME types, bundle order, and licenses |

The WAP also references DynamicData and Entity, but no conclusion follows from
the references alone. Rehost's unsupported DynamicData/Data.Linq surfaces become
blockers only if the bounded journey reaches their types. Conversely, Web API
and Razor are probable blockers because source shows unconditional registration
and the selected admin route directly consumes them.

## Startup and first request

This trace is inferred from source and has not executed on Rehost.

1. Rehost validates and atomically publishes the staged configuration. The
   WAP assembly and rebuilt Core load from `bin`.
2. `Global.asax` is inline. At every `BeginRequest` it calls
   `BlogEngineConfig.Initialize`; before handler execution it applies blog
   culture ([source](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/Global.asax#L1-L16)).
3. The one-shot initializer loads extensions, registers Optimization bundles,
   registers two Web API routes, verifies a SimpleInjector container, sets the
   Web API dependency resolver, and registers jQuery
   ([initializer](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/AppCode/App_Start/BlogEngineConfig.cs#L20-L50),
   [API/DI](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/AppCode/App_Start/BlogEngineConfig.cs#L196-L240)).
4. Configured managed modules run: www canonicalization, URL rewrite,
   compression, referrer tracking, security, and rights
   ([config](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/Web.Config#L125-L155)).
5. URL rewriting resolves the current blog and rewrites home, post, page,
   category, author, calendar, search, and admin paths. API paths are explicitly
   passed to Web API
   ([rewrite](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.Core/Web/HttpModules/UrlRewrite.cs#L48-L102)).
6. The default XML providers lazily read blog settings, posts, users, roles and
   other state from the current blog's `App_Data` storage. A page/master/theme
   renders, bundles and custom `.axd` resources are requested, and the modules
   complete the response.

The first probable blocker is step 3, not page rendering: Web API route and DI
registration are unconditional even for anonymous `/`. A partial port cannot
declare success by bypassing them without recording an application-visible
scope change. If registration can be retained with a narrow host adapter,
anonymous Web Forms, module order, XML providers, rewrite, bundles and assets
are plausible against current Rehost evidence.

## Storage, providers, and restart persistence

The shipped configuration defaults both blog data and file storage to XML and
selects a single-blog usage scenario
([config](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/Web.Config#L3-L25)).
This is the smallest faithful target: no database server, LocalDB, ADO.NET
factory registration, schema installer, or EF layer is needed.

The XML provider writes domain state directly below `App_Data`; inserting a
post creates its directory and rewrites the post XML, including publication and
comment moderation fields
([provider](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.Core/Providers/XmlProvider/Posts.cs#L36-L43),
[write](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.Core/Providers/XmlProvider/Posts.cs#L90-L205)).
Therefore the normal immutable application generation needs an explicit mutable
data boundary. Copying the seed into a disposable writable staged tree is fine
for tests; deployment needs host-owned persistent storage mounted or copied
forward across process generations. Process restart must retain content, users,
roles, settings, comments and uploaded files while replacing binaries/config
atomically.

SQL Server, SQL CE, SQLite and MySQL setup directories exist. Their sample
configs switch to `DbBlogProvider`; some also depend on Framework-era provider
factory configuration and binaries. They add no value to the first slice.
Later SQL Server work could compare provider behavior with Wingtip, but the XML
path is the distinctive portability test.

## Authentication, membership, roles, session, and crypto

The release config uses forms authentication with `.AUXBLOGENGINE`, a literal
SHA1/AES machine key, XML membership, and XML roles. Pages disable session state
globally
([config](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/Web.Config#L90-L118)).
The login page delegates credentials and remember-me behavior to BlogEngine's
security layer
([login](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/Account/login.aspx.cs#L24-L66)).

This aligns better than DNN with current Rehost: forms cookies and literal
machine keys are supported, and session is not required by normal pages. It
still adds meaningful new evidence because the real XML membership/role
providers, password hashing, role-based rights, long-lived sliding ticket, and
logout are untested. The journey must prove successful and rejected login,
admin denial when anonymous, author rights, cookie issuance/expiry, and ticket
validity after process restart. The checked-in key must never be treated as a
production secret; a fixture XDT may use an explicit non-production key, while
deployment injects host-owned key material.

The configured WCF `AuthenticationService` and ASP.NET application-service
endpoint are outside the first journey. Rehost's application-services support
is partial; disable/remove the endpoint by XDT only if evidence shows the UI
does not reach it, and record that boundary.

## Publishing, comments, moderation, search, and feeds

The public reading path is conventional Web Forms plus custom rewriting. Posts
carry `IsPublished`; the XML record contains comments and their approved/spam/
deleted/moderator state. Search is an in-process Core service over loaded
publishables, not an external index. RSS/Atom generation is handled by
`syndication.axd` and custom handlers registered in both classic and integrated
configuration.

The admin mutation path is materially different. The Razor page is an Angular
host (`ng-view`) and renders the `blogadmin` bundle
([admin shell](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/admin/index.cshtml#L1-L8)).
`PostsController` uses Web API for create/update/delete
([posts API](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/AppCode/Api/PostsController.cs#L10-L81));
`CommentsController` performs moderation and bulk actions the same way
([comments API](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/AppCode/Api/CommentsController.cs#L10-L92)).
Thus a test that edits XML directly would bypass the product behavior and would
not satisfy the requested journey.

Initial acceptance should prove:

- anonymous home, one published post, permalink rewrite, search result, and
  RSS/Atom entry;
- login through the real XML membership provider;
- create draft, edit, publish through the admin UI/API, then anonymous access;
- anonymous comment submission with moderation enabled, anonymous invisibility,
  admin approval, then visibility;
- logout and authorization denial; and
- process restart preserving content/comment/user/role state and auth ticket.

Trackback, pingback, MetaWeblog, BlogML, OPML/APML/RSD/SIOC/FOAF, rating, page
services, and outbound pings are not needed for this proof. Their configured
handlers should either load harmlessly or be explicitly excluded by staged XDT;
mere registration is not a support claim.

## Modules, handlers, routing, and IIS assumptions

BlogEngine registers six managed modules and a large custom handler list under
both classic `system.web` and integrated `system.webServer`
([integrated list](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/Web.Config#L177-L212)).
Rehost supports merged managed modules/handlers with boundaries, so the basic
shape is plausible. Reached handlers for the first journey are Web Forms,
extensionless/API routing, bundles, `syndication.axd`, and likely custom
JavaScript/resources. Each must be traced rather than inferred.

The integrated extensionless row names `TransferRequestHandler`, while current
Rehost marks native handler execution and `TransferRequest`-style behavior
bounded or unsupported. Web API WebHost normally depends on ASP.NET routing and
handler integration. This is part of the critical Web API gate; simply deleting
the row may change route semantics. The custom URL rewrite module uses managed
`RewritePath`, which is supported in principle but needs raw/canonical URL and
subfolder evidence.

`system.webServer/staticContent` adds MIME mappings and long cache headers.
Rehost does not honor all `system.webServer` sections. The host/staging layer
must supply correct `.woff`, `.woff2`, `.svg`, `.ico` MIME types and decide
cache policy explicitly; silent config tolerance is not evidence.

## Images, filesystem, case, and separators

Image work is reached by useful author flows. Profile upload calls
`System.Drawing.Image.FromStream`, creates an 80x80 thumbnail, and saves it;
ordinary image/file uploads pass through the BlogEngine file provider
([upload](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.NET/AppCode/Api/UploadController.cs#L45-L115)).
Core image editing and thumbnail delivery use `Bitmap`, `Graphics`, image
formats, resize, crop, flip and rotate
([image service](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.Core/Services/FileSystem/Image.cs#L1-L21),
[resize](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.Core/Services/FileSystem/Image.cs#L109-L170)).
Modern cross-platform .NET cannot treat that as a portable contract. Initial
authoring may omit image upload only if the bounded UI can do so; otherwise a
portable imaging substitute is required and is an architecture decision.

The main XML post paths use `Path.DirectorySeparatorChar`, but static scanning
also finds hard-coded backslashes in the updater, package filesystem mapping,
and setup updater. The automatic updater constructs `\setup\upgrade` paths and
mutates that tree
([source](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.Core/Data/Services/Updater.cs#L13-L18),
[mutation](https://github.com/BlogEngine/BlogEngine.NET/blob/e81ba7425e860c2265f66911436bd241061274f9/BlogEngine/BlogEngine.Core/Data/Services/Updater.cs#L60-L140)).
Those are probably optional under the first journey, but prove cross-platform
unsuitability of live update.

A later audit must scan **all** source and content, including WAP-compiled
`AppCode`, Razor, themes, widgets, extensions, setup scripts, manifests and XML.
It must distinguish regex/URL escapes from physical paths, check authored path
case against the tree, then execute on Linux and the repository's
case-sensitive-filesystem fixture. The AJAX sample demonstrated why excluding
an App-Code-like directory from the scan is unsafe.

## Configuration, extensions, restart, and background work

BlogEngine assumes a writable application tree:

- XML providers mutate `App_Data` for content, identity, roles and settings;
- uploads mutate `App_Data/files` or theme/media directories;
- package, theme, widget and extension installation writes content and can
  alter configuration;
- updater code downloads and replaces setup files;
- config sections request restart on external change; and
- extension loading occurs during first-request initialization.

Only the first category is required initially. It should live in an explicit
persistent data boundary, not turn the whole staged generation mutable.
Packages/themes/widgets/extensions must be preinstalled into a new staged
generation, validated, then published by process replacement. Arbitrary binary
packages also conflict with Rehost's lack of Microsoft `System.Web` binary
identity and need source/rebuild provenance.

Referrer tracking and outbound ping extensions queue ThreadPool work. Those
paths are not core author/read behavior and should be disabled or configured
inert initially. Later support needs shutdown ownership, bounded completion,
duplicate protection, outbound network policy, and process-replacement tests.
No evidence supports treating IIS/AppDomain recycle assumptions as portable.

## Gap comparison with current Rehost

| BlogEngine need | Current evidence | Assessment |
| --- | --- | --- |
| net45 WAP sidecars | WAP `.App`/`.Host`, XDT and staging supported | Strong fit; only two app assemblies |
| Inline `Global.asax`, pages, masters, controls | Broad supported core | Plausible; BlogEngine controls/themes add breadth |
| Managed modules/handlers and `RewritePath` | Supported with stated native/integrated boundaries | Plausible; trace exact order and extensionless/API route |
| Optimization/WebGrease | Partial, including Wingtip production bundles | Plausible; BlogEngine bundle contents/order unproven |
| XML membership/roles | Provider surface partial; real XML-provider journey absent | Critical new proof |
| Forms auth/literal machine key | Supported/partial | Plausible; long sliding ticket and restart must be measured |
| Session | Partial, but BlogEngine pages disable it | Low initial risk |
| Writable XML CMS state | No equivalent stateful app fixture | Critical deployment/data-ownership proof |
| Web API WebHost | No broad support claim | Probable hard blocker for admin author/moderate journey |
| WebPages/Razor execution | No broad support claim | Probable hard blocker for admin shell/editor |
| DynamicData/Entity/Data.Linq | absent/unassessed/unsupported paths | Exclude unless reachability proves otherwise |
| `System.Drawing` | Platform-limited | Blocker only if bounded journey includes image/profile manipulation |
| Custom feeds/search | ASMX/services and handlers partly supported; BlogEngine behavior absent | Valuable bounded evidence |
| Static MIME/cache config | Some `system.webServer` sections unhonored | Host/XDT substitute required |
| Config/bin/content mutation | Immutable generation + process replacement | Architecture decision for installation/update/extensions |
| Background ThreadPool work | No BlogEngine-specific evidence | Disable first; design later |

## Recommended staged journey

### Gate 0 — provenance, payload, and compile inventory

- Freeze v3.3.8.0 for compatibility research and separately record the reviewed
  security patch policy; never expose the unpatched fixture publicly.
- Produce package-based Core and WAP `.App` closure plus a minimal `.Host` and
  staged XDT, without editing upstream.
- Classify every compile failure and every binary as use-as-is, recompile,
  substitute, unsupported, or unassessed. Require reached-path proof for reuse.
- Stage a disposable copy of default XML data; define which `App_Data` paths
  persist across generations.

Exit evidence: deterministic site payload and compile inventory; source clone
unchanged; no application execution in source checkout.

### Gate 1 — anonymous activation

- Cold-start `GET /`, trace initialization, module order, handler, XML provider
  reads, rewrite, status/redirect/cookies and every asset URL.
- Read the seeded post, search for its distinctive term, and fetch RSS/Atom.
- Restart and repeat from the same persistent XML data.

Exit evidence: cold/warm wire transcript and content markers on Windows x64,
Linux, and macOS arm64; case-sensitive filesystem round included.

### Gate 2 — admin substrate

- Prove `/Account/login.aspx`, XML membership/roles, forms ticket and anonymous
  admin denial.
- Implement or adopt only the WebPages/Razor, Web API WebHost, DI, routing,
  JSON and bundle closure reached by `/admin/index.cshtml`, the post editor and
  comments screen.
- Do not widen to general MVC, arbitrary Razor themes, WCF application services,
  remote publishing, or every controller.

Exit evidence: rendered admin shell and direct authorized/unauthorized API
transcripts. Stop if this requires a broad new runtime subsystem without
approval.

### Gate 3 — stateful publishing journey

- Log in; create an unpublished text-only post; edit; publish.
- Verify anonymous permalink, home listing, search and feed.
- Submit an anonymous comment under moderation; prove hidden, approve through
  admin, prove visible.
- Log out; restart process; prove post, comment, user/roles and authorization
  persist, and the explicit-key auth ticket remains valid.

Exit evidence: actual rendered forms/API routes, XML files changed only in the
writable data boundary, and three-platform smoke coverage.

### Gate 4 — one optional portability driver

Choose exactly one after the core journey:

- image upload/thumbnail through a portable imaging substitute; or
- one SQL Server `DbBlogProvider` deployment; or
- one preinstalled theme/extension in a newly published generation.

Image work adds the most cross-platform value. Do not combine all three.

## Effort and uncertainty

| Workstream | Expected effort | Main uncertainty |
| --- | --- | --- |
| Two-project SDK/sidecar conversion | Medium | old Framework APIs in Core, not project count |
| Anonymous activation/modules/rewrite | Medium | unconditional Web API/DI registration may fail early |
| XML provider and mutable-data ownership | Medium | atomic writes, caches, generation boundary, path case |
| XML membership/roles/forms auth | Medium | provider API completeness and password/ticket behavior |
| Admin WebPages/Razor + Web API | High to very high | current Rehost support absent; true minimum closure unknown |
| Post/comment journey | Medium after admin substrate | Angular/API/auth integration and XML cache invalidation |
| Bundles/assets | Medium-low | old dependencies, order, MIME/cache config |
| Portable imaging | Medium-high, deferrable | breadth of edit/thumbnail behavior and output fidelity |
| Database providers | Medium-high, deferrable | factory/config/provider versions and schema variance |
| Package/update/extension lifecycle | High, excluded | live tree/config/bin mutation and untrusted binaries |
| Cross-platform path/case cleanup | Medium | WAP `AppCode`, setup, themes/widgets and authored content |
| Security revision policy | Medium, mandatory before deployment | stable tag predates later upstream fixes |

The strongest uncertainty reducers are: compile the two source-bound
assemblies; activate anonymous `/` through unconditional initialization; then
render `/admin/index.cshtml` and drive one authorized Web API read. Those three
probes decide whether BlogEngine stays a bounded application bring-up or turns
into a Web API/WebPages runtime program.

## Decisions requiring approval

1. Security provenance: frozen v3.3.8.0 plus explicit patch overlay versus a
   reviewed unreleased master snapshot.
2. Add the minimum Web API WebHost and WebPages/Razor substrate versus adapt the
   application administration surface. The latter is a product fork, not a
   compatibility port.
3. Persistent mutable `App_Data` ownership across immutable application
   generations.
4. Portable imaging substitute versus text-only first journey.
5. Preinstalled curated themes/extensions only versus a later transactional
   installation contract.
6. Root single-blog only versus multi-blog host/path routing.

## Unknowns requiring evidence

- First compile failures of Core and WAP against current Rehost packages.
- Whether unconditional Web API registration can activate before WebHost
  handler support exists, and which exact `System.Web.Http.WebHost` APIs bind.
- Minimum WebPages/Razor closure for the admin layout and configured editor.
- Whether SimpleInjector 3.1's execution-context scoping works unchanged.
- Exact request authorization mechanism for each selected API controller at the
  pinned revision, including later security-fix implications.
- XML membership password compatibility and concurrent/atomic XML writes.
- Cache invalidation after post and comment mutation and after restart.
- Custom handler behavior for syndication, generated resources and images.
- Bundle debug/production behavior and case-sensitive glob ordering.
- Reached Framework-only APIs in `BlogEngine.Core`, especially Data.Linq,
  DirectoryServices, ServiceModel, WCF application services and System.Design.
- Physical path separator and case defects across all source/content.
- Correct static MIME/cache behavior without relying on ignored IIS sections.
- Graceful shutdown while queued referrer/ping work is running.
- Release asset versus repository payload differences and licenses for vendored
  binaries/assets.

Until these are answered, the correct claim is **static feasibility for an
anonymous XML-backed blog, with probable Web API/Razor blockers on the real
authoring journey**, not “BlogEngine.NET runs on Rehost.”
