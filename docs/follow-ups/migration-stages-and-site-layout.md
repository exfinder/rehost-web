# Migration stages and the site layout

A host project that sets `RehostSiteContentRoot` keeps the Web Forms site in
`rehost_root/` beside `Program.cs`, and compiles into `rehost_root/bin/`:

```text
MyApp.Host/
  rehost_root/        git-ignored while it is a copy
    bin/              host, runtime and application assemblies (OutDir)
    *.aspx, web.config, Content/, Scripts/
  Program.cs
  Web.Rehost.config
  MyApp.Host.csproj
```

The folder keeps one address through the whole migration. What changes between
stages is whether it is a copy or the source.

## Stages

1. **Side by side.** The Framework project is alive in its own folder and the
   port reads it through `RehostAppContentRoot` (the `*.cs`, compiled by the
   App project) and `RehostSiteContentRoot` (the content, copied to
   `rehost_root/` at build). Neither build writes a file the other owns.
   `rehost_root/` is a copy because both runtimes require `<root>/bin`, and
   the Framework folder's `bin/` holds Framework-built assemblies under the
   same file names the port needs.
2. **Port only.** `git mv` the content into `rehost_root/`, delete the old
   csproj, remove the ignore line. The App project owns the `*.cs` directly.
   The published shape was already this one.
3. **Coexistence with ASP.NET Core.** Razor pages and components compile into
   the host assembly; `wwwroot/` and `Pages/` sit beside `rehost_root/` in the
   project, not under it. Core endpoints run first and Web Forms is the
   fallback. A page migrates by adding the Razor page at the same URL and
   deleting the `.aspx`.
4. **Done.** The last `.aspx` is gone; `rehost_root/`, the package references
   and these targets come out, leaving the stock flat ASP.NET Core publish.

Stage 3 needs runtime work that does not exist yet. `UseRehostWebForms()` is
terminal (`app.Run`), so an unmapped path gets System.Web's 404 rather than
falling through; coexistence needs a fall-through mode or a `MapWhen` on the
Web Forms extensions. The cost that dominates is shared state: one
authentication cookie both sides accept and one session store both read.

## Why App and Host are two projects

The App project is the old web project's DLL; the Host is the process that
replaced IIS. One project can carry both properties, but each reason below is
a cost every migrator would then meet alone.

- **Compiler settings.** The legacy sources need `Nullable` and
  `ImplicitUsings` off, a pinned `LangVersion` (7.3 for an old tree, 13 for
  YAF) and `GenerateAssemblyInfo=false`, because the tree has its own
  `AssemblyInfo.cs`. `Program.cs` uses top-level statements, which need C# 9.
  A project has one set of settings.
- **Name clashes.** The Web SDK imports `Microsoft.AspNetCore.Http` into every
  file, and the legacy sources import `System.Web`. Both declare `HttpContext`,
  `HttpRequest` and `HttpResponse`, so an unqualified use is ambiguous (CS0104).
- **Assembly identity.** Configuration names the application assembly:
  `type="WingtipToys.Models.ProductContext, WingtipToys"`, the OWIN startup key,
  `<add assembly="..."/>`. The App project keeps that name through
  `AssemblyName`; a single project would have to give it to the executable.
- **No ASP.NET Core in the application.** An App project restores with
  `Microsoft.NETCore.App` alone. `Microsoft.AspNetCore.App`, and with it
  Kestrel, enters only through `Rehost.WebForms.Hosting`, which only the Host
  references. The application code sees `System.Web` and nothing of the server
  beneath it.
- **References.** A migrator's test projects reference the App library as they
  referenced the WAP's DLL. Multi-project applications fit the same way: YAF's
  fourteen libraries and its App are all references of one Host.
- **The end of the migration.** Stage 4 deletes the App project and
  `rehost_root/` and keeps the Host. Host-only code, such as WingtipToys' local
  PayPal responder, never enters the application assembly.

## Two roots, both derived from the binaries

A host has a Web Forms root and an ASP.NET Core content root, and neither may
depend on the directory the process was started from:

- `PhysicalRootPath` is the parent of `AppContext.BaseDirectory`: the site
  root, `rehost_root/` in development and the published folder in deployment.
- `ContentRootPath` is `AppContext.BaseDirectory` itself, the site's `bin/`.
  The Web SDK copies `appsettings.json` there, and publishes `wwwroot/` there,
  so that is where ASP.NET Core must look. Left at its default the content
  root is the working directory, which `dotnet run` sets to the site root and
  a deployment sets to anything; YAF's Serilog settings went unread that way.

## Why the project sets `OutDir`

The compiler writes the payload straight into the site's `bin/`, so it exists
once. Before, the build wrote it to `bin/Debug/net10.0/` and the targets copied
all of it into the stage (79 files, 33 MB for the stock template, twice).

The package targets cannot set `OutDir` themselves. The SDK derives
`TargetDir`, `TargetPath`, and the deps and runtimeconfig paths from `OutDir`
before package targets import. A probe project with a late `OutDir` override
(measured 2026-09-19, SDK 10.0.302) kept all four on `bin/Debug/net10.0/`:
the build failed in `GenerateDepsFile` on the missing folder and `dotnet run`
looked for the program there. A package `.props` imports early enough but
before the project body, so it cannot see `RehostSiteContentRoot` and would
move the output of every project that references the package.

The project therefore carries `<OutDir>rehost_root/bin/</OutDir>`, and
`RehostVerifyOutDir` fails the build with that exact line when `OutDir` does
not resolve to the stage's `bin/`.

Debug and Release share `rehost_root/bin/`. Publish keeps its own tree under
`bin/<Configuration>/<tfm>/site-publish/` and is unaffected. `dotnet publish`
never deletes files an earlier publish wrote, so delete the publish folder
before judging its contents.

## Publishing to a chosen folder

`dotnet publish -o X` and `-p:RehostPublishSiteRoot=X/` both name the published
site's root: content and `web.config` land in `X/`, the payload in `X/bin/`.
`-o` needs help to get there. A command-line `PublishDir` wins over the value
the targets assign, so the payload used to land flat in `X/` while the content
went to the default `site-publish/`, and neither half could run. The targets
recognize a command-line `PublishDir` by assigning a probe value that does not
stick, take that folder as the site root, and `RehostRedirectPublishDir` points
`PublishDir` at its `bin/` before the SDK copies anything; a target may
reassign a global property where evaluation may not. Naming two different
folders through `-o` and `RehostPublishSiteRoot` fails the publish.

The stage manifest, which lets a later run delete files whose sources are gone,
is kept per stage root. One manifest per project made a publish to one folder
delete the content an earlier publish had staged in another.

## Why the hosts use `Microsoft.NET.Sdk.Web`

For its runtime defaults, server GC first: the built `runtimeconfig.json`
carries `System.GC.Server: true`. The Web SDK brings three behaviors the
hosting targets answer:

- its default globs would compile and ship the stage's files, so the stage is
  added to `DefaultItemExcludes`;
- its `**/*.config` content glob would copy `Web.Rehost.config` beside the
  payload, so the targets move that file from `Content` to `None`;
- its publish step would write an ASP.NET Core Module `web.config` into the
  published `bin/`, so `IsWebConfigTransformDisabled` is set. The site's own
  `web.config` at the root is the one that applies.

## Candidate: splitting Host and App files

Host and runtime files flat beside `Host.dll`, the application's closure alone
in `rehost_root/bin/`: the old GAC-versus-`bin` division, with no change to
imported code. Two prototypes (2026-09-19, macOS) settle the mechanism.

- Loading the application side by file name does not hold. The stock template
  and the Identity application passed their smokes, but
  `Microsoft.Data.SqlClient` threw `PlatformNotSupportedException`: the package
  ships a stub at `lib/` and the real assemblies under `runtimes/unix` and
  `runtimes/win`, and a file-name probe takes the stub. Native libraries needed
  a hand-written resolver too.
- `AssemblyDependencyResolver` over the application's own `deps.json` does
  hold. The App builds with `EnableDynamicLoading`, and the default context's
  `Resolving` and `ResolvingUnmanagedDll` handlers ask the resolver. SqlClient
  loaded from `runtimes/unix/lib` and reached SQL Server, SQLite loaded its
  native library, and the host folder held host files only. This is the .NET
  plugin contract, so the host's resolution rules are reused, not copied.

How the Host refers to the App decides the rest (probe with Newtonsoft.Json 12
on the host side and 13 on the application side):

- No reference: two NuGet graphs. The build passes and the App fails at run
  time with `FileLoadException`, because the host side's 12 loads first.
- Plain `ProjectReference`: one graph, so NuGet stops the clash at restore
  (NU1605). Once fixed it runs, but `App.dll` is copied beside the Host and
  that copy loads; `bin/` holds dead duplicates, and building the App alone
  leaves the loaded copy stale.
- `<ProjectReference ... Private="false" ExcludeAssets="runtime" />`: one
  graph and the same NU1605 guard, `App.dll` absent from the host folder and
  from `Host.deps.json`, loaded from `bin/`; shared libraries load from the
  host side at the unified version. Host code compiles against App types,
  which coexistence needs anyway. This is the shape to use, and the targets can
  refuse a plain reference the way `RehostVerifyOutDir` refuses a wrong
  `OutDir`.

Measured on the prototype (2026-09-19/20), with the resolver wired into
`GeneratedAssemblyLoader` ahead of the file-name probe:

- The reference needs `ExcludeAssets="runtime;native"`; `runtime` alone lets
  native assets into the host folder.
- A multi-project App marks each of its direct project references
  `PrivateAssets="all"`, or its libraries land on both sides and load from the
  host folder. The flag on the App alone hides the whole chain behind it; the
  libraries stay unchanged (YAF: seven lines in `YAF.App.csproj`).
  `DisableTransitiveProjectReferences` on the Host also stops the leak but
  applies to every reference the Host has: a new ASP.NET Core project's own
  dependencies are then copied yet missing from `Host.deps.json`, so their
  native and per-OS assets stop resolving and the Host cannot compile against
  them.
- The stock template, the Identity application and YAF (install plus 59
  checks) pass. App-only `Microsoft.Data.SqlClient` and SQLite work from a
  compiled page, and the resolver probe passes on macOS, Linux and Windows,
  choosing `runtimes/win` or `runtimes/unix` correctly.
- Publish is two ordinary publishes, the Host to `X/` and the App to
  `X/rehost_root/bin/`, portable or for one runtime identifier; both pass the
  smoke.
- `AppDomain.CurrentDomain.BaseDirectory` becomes the host folder, and YAF's
  module scanner then finds no provider. Setting `APP_CONTEXT_BASE_DIRECTORY`
  to `<root>/bin/`, today's value, fixes it; eShop expects the site root there,
  as Framework had it, which is already wrong today.
- `RoslynCSharpCompiler` prefers an out-of-band copy of a framework assembly
  only when it sits in `AppContext.BaseDirectory`; an App-side copy would be
  missed.
- The resolver must be installed before any method that names an App type is
  compiled, which for Razor pages using App models means process start.
- `bin/` still receives the runtime's files as unused duplicates, because the
  add-on packages depend on the runtime; an `ExcludeAssets` on the App's
  package reference does not trim them. Deleting from `bin/` what the host
  folder holds does (18 files for the stock template).
- Start time is unchanged (cold 1.9 s against 1.7 s, warm 0.8 s against
  0.7 s). After a Host-only rebuild the split reuses every compiled page
  (0.6 s) where the classic layout recompiles them (1.6 s).

Open before it could ship: what `BaseDirectory` should be, who trims `bin/`,
where the resolver is installed, the targets' development and publish wiring,
and assemblies dropped into `bin` outside the App's closure, which still need
the file-name probe.

## Open

- The edit-markup-refresh loop. In stage 1 a source `.aspx` reaches the stage
  on the next build. [In-place dev run](in-place-dev-run.md) covers both
  answers: a content sync into `rehost_root/`, and a host-provided bin seam.
  From stage 2 on the loop is native, because the runtime serves the source
  folder and recompiles a page when its markup changes.
- The fall-through mode and the state bridges for stage 3.
- Web Site hosts still carry their own `*.cs` staging step
  ([project models](web-site-vs-wap-project-models.md)).
