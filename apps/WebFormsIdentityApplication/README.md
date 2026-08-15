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

| `packages.config` | Here | Note |
| --- | --- | --- |
| `Microsoft.Owin.Host.SystemWeb` 4.2.2 | `Rehost.WebForms.Owin.Host.SystemWeb` | The one binary that had to be recompiled: it binds Microsoft's strong-named `System.Web`. Katana 4.2.3 source; see [provenance](../../docs/provenance/aspnet-katana.md) |
| `Microsoft.Web.Infrastructure` 2.0 | dropped | Only Katana's `DynamicModuleUtility` dependency, and Katana 4.x calls `HttpApplication.RegisterModule` directly |
| `Microsoft.AspNet.Web.Optimization`, `.WebForms`, `Microsoft.AspNet.FriendlyUrls*`, `Microsoft.AspNet.ScriptManager.*` | `Rehost.WebForms.Optimization`, `.Optimization.WebForms`, `.FriendlyUrls`, `.ScriptManager.Bundles` | Same mapping as `WebFormsApplication` |
| `Microsoft.CodeDom.Providers.DotNetCompilerPlatform` | dropped | The runtime owns compiler selection; `<system.codedom>` is removed by XDT |
| `Owin`, `Microsoft.Owin`, `.Security`, `.Security.Cookies`, `.Security.OAuth`, `.Security.Google`, `.Security.Facebook`, `.Security.Twitter`, `.Security.MicrosoftAccount` | same packages from nuget.org | Pure managed; consumed as shipped under `NU1701` |
| `Microsoft.AspNet.Identity.Core`, `.Owin`, `.EntityFramework` 2.2.4 | same packages from nuget.org | Pure managed |
| `EntityFramework` 6.4.4 | same package from nuget.org | 6.3+ ships `netstandard2.1`; its SQL Server provider uses `System.Data.SqlClient` |
| `Newtonsoft.Json` 13.0.3 | same package, pinned | `Microsoft.Owin.Security` still asks for 6.0.4 (NU1903) |
| Antlr, WebGrease, bootstrap, jQuery, Modernizr | unchanged content/dependencies | Same as `WebFormsApplication` |

`Rehost.WebForms` and `Rehost.WebForms.Hosting` replace what the GAC gave the
Framework app; the host adds the latter.

## Database

The template's `(LocalDb)\MSSQLLocalDB` connection string is the app-visible
change (see boundaries). Any reachable SQL Server works; a container is the
short path:

```text
docker run -d --name rehost-identity-sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Rehost!Dev2026' -p 14333:1433 mcr.microsoft.com/mssql/server:2022-latest
```

Entity Framework creates the database on first use, so the first register POST
takes a few seconds longer than the rest.

## Commands

```text
dotnet build apps/WebFormsIdentityApplication/WebFormsIdentityApplication.slnx
dotnet run --project apps/WebFormsIdentityApplication/WebFormsIdentityApplication.Host
# http://127.0.0.1:5082/ (pass a URL as the first argument to change)

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

One Linux round joins the SQL container's own network namespace, so the single
committed connection string (`127.0.0.1,14333`) needs no second copy:

```text
docker run -d --name rehost-identity-sql-linux -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Rehost!Dev2026' -e MSSQL_TCP_PORT=14333 mcr.microsoft.com/mssql/server:2022-latest
eng/identity-linux-smoke.sh   # builds and smokes the committed HEAD in that namespace
```

## web.config

`WebFormsIdentityApplication.Host/Web.Rehost.config` replaces the package
default wholesale, so it repeats the default's three rules — remove `<runtime>`,
remove `<system.codedom>`, retarget the Optimization `<controls>` assembly — and
adds one of its own: the `DefaultConnection` connection string. Nothing else in
the template's `web.config` needed a transform. `<sessionState>` naming a
provider type from `System.Web.Providers`, which is not in `bin`, parses and
activates exactly as it does on Framework, and the unhonored
`<system.webServer><modules><remove name="FormsAuthentication" />` is ignored
the same way.

## Boundaries

- **LocalDb is out of contract.** It is a Windows-only SQL Server flavour, so
  the connection string is the one application-visible edit a migration must
  make. `|DataDirectory|` itself is fine — the runtime points it at `App_Data`.
- **`<machineKey>` is auto-generated.** The template declares none, so the key
  protecting `.AspNet.ApplicationCookie` is random and process-scoped: every
  restart invalidates every issued cookie, and two processes cannot share them.
  The runtime raises a diagnostic naming that consequence. An application that
  needs sign-ins to survive a restart adds an explicit `<machineKey>`.
