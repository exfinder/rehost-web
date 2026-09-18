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
   the host assembly; `wwwroot/` and `Pages/` sit beside `rehost_root/`, not
   under it, because the ASP.NET Core content root is the host project folder
   and the Web Forms physical root is `rehost_root/`. Core endpoints run
   first and Web Forms is the fallback. A page migrates by adding the Razor
   page at the same URL and deleting the `.aspx`.
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

## Open

- The edit-markup-refresh loop. In stage 1 a source `.aspx` reaches the stage
  on the next build. [In-place dev run](in-place-dev-run.md) covers both
  answers: a content sync into `rehost_root/`, and a host-provided bin seam.
  From stage 2 on the loop is native, because the runtime serves the source
  folder and recompiles a page when its markup changes.
- The fall-through mode and the state bridges for stage 3.
- Web Site hosts still carry their own `*.cs` staging step
  ([project models](web-site-vs-wap-project-models.md)).
