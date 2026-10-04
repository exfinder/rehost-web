# BlogEngine.NET development

[Running guide](README.md) · [Imported sources](../../docs/dev/sources.md)

## Layout

`BlogEngine/` holds upstream's `BlogEngine.Core` and `BlogEngine.NET` folders,
with redistribution notices added to the site; existing upstream files are
unedited. `lib/` holds the two upstream binaries `BlogEngine.Core` references.
Three projects build them:

- `BlogEngine.Core` compiles the class library as `BlogEngine.Core.dll`;
  `web.config` names its providers, modules and handlers by that name. It is the
  worked example for [class libraries that use System.Web](../../docs/migration.md#class-libraries-that-use-systemweb).
- `BlogEngine.App` compiles the web project as `BlogEngine.NET.dll`, the
  assembly `web.config` registers page controls from.
- `BlogEngine.Host` owns Kestrel, the `web.config` transform and the
  database seed.

## Changes BlogEngine needed

Each replacement is a copy of the upstream file under `BlogEngine.Core/`, with
a `Compile Remove` line for the original.

- `Packaging/Gallery.cs`, `Installer.cs`, `FileSystem.cs`: the extension installer
  was written against NuGet.Core 2.x, which has no .NET build. The copies use
  NuGet.Protocol through `Packaging/NuGetFeed.cs`. The gallery server is gone,
  so they are only compiled.
- `Providers/DbConnectionHelper.cs`: the database providers' SQL spells some
  parameters in a different case than the code adds them (`{1}Author` and
  `author`, `{1}blogId` and `blogid`). System.Data.SQLite 1.0 matched names
  case-insensitively; 2.x does so only with `NoCase` set, which the copy sets
  on every command. Without it, saving a post, which every new comment does,
  fails with "Insufficient parameters supplied to the command".
- `Compat/SystemLinqDynamic.cs`: DynamicQuery 1.0 is no longer on nuget.org.
  System.Linq.Dynamic.Core has the same string-query extension methods under a
  new namespace; the file imports it globally and declares the old one, so the
  repositories' `using System.Linq.Dynamic;` still compiles.
- Excluded from the compile: `Services/Compilation/Design/` (Visual Studio
  design-time editors from `System.Design`), `Service References/` (a WCF Data
  Services client nothing calls), and five files the legacy project never
  compiled (`CategoryDictionary.cs`, `Profile.cs`, `Scripting/ContentItem.cs`,
  `Web/AppConfig.cs`, `Web/InstallUtil.cs`).

## Dependency choices

- `packages.config`, line by line: Web Optimization, Web API WebHost and Web
  Pages use their Rehost packages; Web API Core and Client and Razor arrive
  through them. Microsoft.Web.Infrastructure is dropped; `Rehost.Web` supplies
  its API. The script and style packages need nothing: their files are in the
  imported tree.
- SimpleInjector 3.1.0's only build is `net45`; it restores under `NU1701` and
  then fails the first Web API request with `MissingMethodException`
  (`LambdaExpression.CompileToMethod`). SimpleInjector 4.10.2 has a .NET Standard
  build and keeps 3.x's implicit controller resolution, which 5.x turns off.
  `SimpleInjector.Extensions.ExecutionContextScoping` is unused and dropped.
- `NU1701` is silenced per package on Antlr, WebGrease,
  Microsoft.AspNet.Razor, Microsoft.AspNet.WebApi.Core and
  SimpleInjector.Integration.WebApi. The middle two arrive through Rehost
  packages and are referenced directly to carry the suppression.
- LINQ to SQL: Mindbox.Data.Linq keeps the `System.Data.Linq` namespaces, so
  the database file store compiles unchanged. It is only compiled; the file
  store stays XML.
- System.Drawing.Common and System.ServiceModel.Syndication supply types that
  were part of .NET Framework.
- AjaxMin and BlogML are upstream's .NET Framework binaries from `lib/`;
  BlogML 2.5 has no package. Their code paths are untested.
  SharpZipLib 1.4.2 restores from NuGet under MIT, replacing upstream's
  GPL-licensed 0.86 DLL without changing the imported callers.
  [Redistribution notices](BlogEngine/BlogEngine.NET/licenses/README.md)
  accompany the staged and published site.
- SQLite: System.Data.SQLite 2.0.4 with SQLitePCLRaw.lib.e_sqlite3, which carries
  the native library for each platform. The DLL in upstream's `setup/SQLite` is
  a Windows .NET Framework build and is not imported. BlogEngine loads the
  driver by invariant name; `BlogEngine.Core` references it for the `NoCase`
  setting above.

## Configuration

`BlogEngine.Host/Web.Rehost.config`:

- Removes `<runtime>` and `<system.serviceModel>`, which declares a WCF
  authentication service with no `.svc` behind it.
- Removes the `System.Management` and `System.Net.Http.WebRequest` compilation
  assemblies, which .NET does not ship and no page uses.
- Applies the differences between `setup/SQLite/SQLiteWeb.Config` and
  `Web.Config`: the database blog, membership and role providers, the
  `BlogEngine` connection string and the `DbProviderFactories` row. The
  connection string writes `|DataDirectory|/BlogEngine.s3db`; with upstream's
  backslash, SQLite creates an empty `\BlogEngine.s3db` beside the real file
  off Windows. The file store stays in `App_Data`, as in `SQLiteWeb.Config`.

`<handlers accessPolicy="Read, Write, Script, Execute">` and the explicit
`<machineKey>` pass through unchanged, so sign-ins survive host restarts.

The Host build copies `setup/SQLite/BlogEngine.s3db` to the staged `App_Data`
when it is missing. Staging never overwrites an `App_Data` file that exists,
so posts, comments and users survive rebuilds.

## Validation

`smoke.sh` signs in with BlogEngine's defaults and covers the public pages, the
seven `.axd` feeds, Web API reads and writes, a comment through
`WebForm_DoCallback` checked in the database file, and both sign-out paths. It
passes on macOS arm64 and Linux arm64, and runs in the CI `apps` job on Linux
x64. Windows is not yet validated.

```text
eng/app-linux-smoke.sh BlogEngine 5086
```

## Open application scope

`image.axd` resizing (System.Drawing) and the admin **About** page
(`WindowsIdentity.GetCurrent()`) are Windows-only. SQL Server and MySQL storage,
the extension gallery, BlogML import and export, the `BinaryFormatter` deep copy
in `ManagedExtension`, and mail remain unassessed. Runtime support belongs to
[compatibility](../../docs/dev/compatibility.md).
