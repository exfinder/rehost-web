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

- `PhysicalRootPath` is the parent of `AppContext.BaseDirectory` as `Program.cs`
  reads it at start: the site root, `rehost_root/` in development and the
  published folder in deployment.
- `ContentRootPath` is that start value itself, the site's `bin/`.
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

## Split layout: measured, not adopted

Host files flat beside `Host.dll` and the application's closure alone in
`rehost_root/bin/`, the old GAC-versus-`bin` division, was prototyped three ways
on 2026-09-19. The layout above stays.

- **Two NuGet graphs** (the Host references the App compile-only, the App hides
  its libraries with `PrivateAssets="all"`, `AssemblyDependencyResolver` loads
  `bin/` from the App's own `deps.json`). Everything built, and YAF failed at
  run time with `FileLoadException`: a library pinned
  `System.Runtime.Caching` 10.0.11 while the host side carried 10.0.10, and
  neither restore saw the other. Two graphs in one process is the Framework
  binding-redirect problem again. Rejected: one process has one NuGet graph.
- **One graph, two manifests.** Without `PrivateAssets`, NuGet resolves one
  graph and its `ExcludeAssets` flags already mark what is reached only through
  the App; NU1605 stops a downgrade at restore. The SDK ignores the flag for
  transitive project references, so their DLLs land beside the Host unless a
  target re-marks them, and the resolver and its install before `Main` remain.
- **One graph, one manifest** ([`poc/SplitDependencyGraph`](https://github.com/exfinder/rehost-webforms/tree/cf1910790beb5aafbf999e964a907172992e5f6e/poc/SplitDependencyGraph),
  since removed from the tree).
  The .NET 10 host reads an optional `localPath` on each `deps.json` asset, so
  `bin/` files sit on the trusted-assembly list like any other: no resolver, no
  second `deps.json`, one copy of every file. A task sets
  `DestinationSubDirectory` on the App's closure and a second one adds
  `localPath` to the SDK's `deps.json`. Build, publish (portable, for one
  runtime identifier, self-contained) and per-OS and native assets
  (`Microsoft.Data.SqlClient` on macOS, Linux and Windows) all passed.

Why the last one still waits:

- It moves few files. The App references `Rehost.WebForms`, so the runtime and
  Roslyn belong to its closure; the host folder would keep `Host.dll`, the
  hosting assembly and the Host's own packages. Those are harmless in `bin/`:
  pages do not import their namespaces, and `/bin` is never served.
- It costs two MSBuild tasks shipped in a package, a rewrite of an SDK output, a
  private SDK item name (`_ResolvedCopyLocalBuildAssets`) for publish, and a
  task assembly that has not been loaded by Visual Studio's MSBuild.
- The SDK writes `localPath` itself from `DestinationSubDirectory` starting with
  .NET 11 (dotnet/sdk#50120; not in any 10.0 band). After that the split needs
  one task that sets documented metadata.

Revisit when the .NET 11 SDK is the floor, or when a stage 3 application shows
that Razor pages, `wwwroot/` and new libraries inside `rehost_root/bin/` hurt.
It would retire the `OutDir` line and its check, the `-o` redirect and the
content-root line. The [Rehost SDK](rehost-sdk.md) could take the `OutDir`
line out of the csproj sooner, and is parked as too little gain on its own.

A reading that holds for either layout: after a Host-only rebuild the split reused
every compiled page (0.6 s) where this layout recompiles them (1.6 s),
because the top-level hash covers `bin/`.

## Open

- The edit-markup-refresh loop. In stage 1 a source `.aspx` reaches the stage
  on the next build. [In-place dev run](in-place-dev-run.md) covers both
  answers: a content sync into `rehost_root/`, and a host-provided bin seam.
  From stage 2 on the loop is native, because the runtime serves the source
  folder and recompiles a page when its markup changes.
- The fall-through mode and the state bridges for stage 3.
- Web Site hosts still carry their own `*.cs` staging step
  ([project models](web-site-vs-wap-project-models.md)).
