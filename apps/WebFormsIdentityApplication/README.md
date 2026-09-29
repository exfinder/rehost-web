# WebFormsIdentityApplication

The Visual Studio 2022 Web Forms template application with **Individual User
Accounts** authentication (register, login, external logins, two-factor,
password reset), running on the ported runtime from packages — the way an
external consumer would.

## Layout

| Folder | Role |
| --- | --- |
| `WebFormsIdentityApplication/` | The frozen .NET Framework 4.8.1 WAP. Never modified; stays buildable in Visual Studio on Windows. `bin/`, `obj/`, `packages/`, the `.sln`, and the LocalDb `App_Data/*.mdf\|ldf` are not imported. |
| `WebFormsIdentityApplication.App/` | The port of the app assembly: compiles the legacy folder's `*.cs` into `WebFormsIdentityApplication.dll`, exactly what the WAP produced in `bin/`. |
| `WebFormsIdentityApplication.Host/` | The process: a ~30-line Kestrel host, plus the app's own `Web.Rehost.config`. |

The migration is the same one
[`WebFormsApplication`](../WebFormsApplication/README.md) shows — every
`packages.config` line becomes a package reference, the legacy folder is never
touched — with two additions: most of this app's dependencies come from
nuget.org unchanged, and the connection string has to move off LocalDb.

## packages.config → PackageReference

Packages built on `System.Web` take the Rehost counterpart the
[package mapping](../../docs/migration.md#package-mapping) names; the table records this
application's decisions.

| `packages.config` | Here | Note |
| --- | --- | --- |
| `Microsoft.Owin.Host.SystemWeb` 4.2.2 | Rehost counterpart | The one binary that had to be recompiled: it binds Microsoft's strong-named `System.Web`. Katana 4.2.3 source; see [provenance](../../docs/provenance/aspnet-katana.md) |
| `Microsoft.Web.Infrastructure` 2.0 | dropped | Only Katana's `DynamicModuleUtility` dependency, and Katana 4.x calls `HttpApplication.RegisterModule` directly |
| `Microsoft.AspNet.Web.Optimization`, `.WebForms`, `Microsoft.AspNet.FriendlyUrls*`, `Microsoft.AspNet.ScriptManager.*` | Rehost counterparts | Same set as `WebFormsApplication` |
| `Microsoft.CodeDom.Providers.DotNetCompilerPlatform` | dropped | The runtime owns compiler selection; `<system.codedom>` is removed by XDT |
| `Owin`, `Microsoft.Owin`, `.Security`, `.Security.Cookies`, `.Security.OAuth`, `.Security.Google`, `.Security.Facebook`, `.Security.Twitter`, `.Security.MicrosoftAccount` | same packages from nuget.org | Pure managed; consumed as shipped under `NU1701` |
| `Microsoft.AspNet.Identity.Core`, `.Owin`, `.EntityFramework` 2.2.4 | same packages from nuget.org | Pure managed |
| `EntityFramework` 6.4.4 | same package from nuget.org, bumped to 6.5.2 | 6.3+ ships `netstandard2.1`. The bump is what the maintained SQLite provider requires (see database) |
| — | `System.Data.SQLite` 2.0.4, `.EF6` 2.0.3, `SQLitePCLRaw.lib.e_sqlite3` | Added by the host, not the app: the store the template pointed at LocalDb |
| `Newtonsoft.Json` 13.0.3 | same package, pinned | `Microsoft.Owin.Security` still asks for 6.0.4 (NU1903) |
| Antlr, WebGrease, bootstrap, jQuery, Modernizr | unchanged content/dependencies | Same as `WebFormsApplication` |

`Rehost.Web` and `Rehost.Web.AspNetCore` replace what the GAC gave the
Framework app; the host adds the latter.

## Database

The template's `(LocalDb)\MSSQLLocalDB` connection string is the app-visible
change (see boundaries). It points at SQLite here — a file under `App_Data`, so
the app needs no server and no second connection string per platform:

```text
Data Source=|DataDirectory|Identity.db;Foreign Keys=True
```

Three things SQLite needs that SQL Server got for free:

- **Name the ADO.NET factory in `<system.data>`.** `<entityFramework><providers>`
  supplies provider *services*; the factory behind them is a
  `<DbProviderFactories>` row that `Web.Rehost.config` adds. It names
  `SQLiteFactory`, not the EF6 provider factory, because EF reverse-maps the
  connection's own factory type back to an invariant name.
- **Create the schema itself.** The EF6 SQLite provider generates no DDL, so
  `Database.Create()` throws instead of building the Identity tables.
  `Identity.schema.sql`, generated once from `ApplicationDbContext`'s model with
  `SQLite.CodeFirst`, is applied to a missing database file at startup, and
  `disableDatabaseInitialization` keeps EF from trying afterwards.
- **Stay out of Entity Framework until the application is up.** Reaching EF
  before `HostingEnvironment` initializes installs the configuration system, and
  initialization then fails with *the configuration system has already been
  initialized*. Hence raw DDL over a `SQLiteConnection` rather than a context.

`SQLitePCLRaw.lib.e_sqlite3` carries the native library for every target,
including `osx-arm64` and `linux-arm64`. This works only on the 2.0.x provider
line: 1.0.x binds the SQLite team's own `SQLite.Interop.dll`, which ships
`win-x86/x64`, `linux-x64`, and `osx-x64` only, and the community arm64 builds
export plain `sqlite3_*` symbols the mangled managed assembly cannot call.

## Commands

```text
dotnet build apps/WebFormsIdentityApplication/WebFormsIdentityApplication.slnx
dotnet run --project apps/WebFormsIdentityApplication/WebFormsIdentityApplication.Host
# http://127.0.0.1:5082/ (add `-- --urls <url>` to change)

apps/WebFormsIdentityApplication/smoke.sh            # against the default URL
apps/WebFormsIdentityApplication/smoke.sh http://127.0.0.1:5082
```

`smoke.sh` is bash + curl only — macOS, Linux, and Git bash on Windows all run
it. It registers a per-run user, signs in and out, and asserts every row of the
IIS Express baseline recorded in
[the gaps document](../../docs/research/webforms-identity-application-gaps.md):
status codes, the absolute `Location` of the challenge redirect, the
`.AspNet.ApplicationCookie` set and later expired, the expired `.ASPXAUTH` that
`LoginStatus` writes, and the page markers. Every row matches; there is no
recorded delta.

One Linux round needs nothing but the runner:

```text
eng/app-linux-smoke.sh WebFormsIdentityApplication 5082   # builds and smokes the committed HEAD
```

## web.config

`WebFormsIdentityApplication.Host/Web.Rehost.config` replaces the package
default wholesale, so it repeats the default's three rules — remove `<runtime>`,
remove `<system.codedom>`, retarget the Optimization `<controls>` assembly — and
adds three of its own: the `DefaultConnection` connection string, the SQLite
entry in `<entityFramework><providers>`, and `disableDatabaseInitialization` for
`ApplicationDbContext`. Nothing else in the template's `web.config` needed a
transform. `<sessionState>` naming a provider type from `System.Web.Providers`, which is not in
`bin`, parses and activates exactly as it does on Framework, and the unhonored
`<system.webServer><modules><remove name="FormsAuthentication" />` is ignored
the same way.

## Boundaries

- **LocalDb is out of contract.** It is a Windows-only SQL Server flavour, so
  the connection string is the one application-visible edit a migration must
  make. `|DataDirectory|` itself is fine — the runtime points it at `App_Data`.
- **SQLite is a fixture choice, not a compatibility claim.** It shows EF6 running
  on the port against a real engine without a server; SQL Server deployment
  belongs to Milestone 3.
- **`<machineKey>` is auto-generated.** The template declares none, so the key
  protecting `.AspNet.ApplicationCookie` is random and process-scoped: every
  restart invalidates every issued cookie, and two processes cannot share them.
  The runtime raises a diagnostic naming that consequence. An application that
  needs sign-ins to survive a restart adds an explicit `<machineKey>`.
