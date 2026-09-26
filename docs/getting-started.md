# Getting started

How to run an existing ASP.NET Web Forms Web Application Project on .NET 10. The
legacy project is not edited and keeps building on .NET Framework. A Web Site
project (no csproj, code in `App_Code`) is not covered by the template yet.

You need the .NET SDK 10.0.302 or a later 10.0.3xx release, which carries the
10.0.10 runtime the alpha was validated with. Nothing else: no database, no
Docker, no IIS, no Visual Studio. Windows x64, Linux x64, Linux arm64 and macOS
arm64 are the validated platforms.

## 1. Install the template

```text
dotnet new install Rehost.WebForms.Templates
```

## 2. Add the App and Host projects

Go to the folder that contains the legacy web application folder. This is
usually the folder of the old `.sln` file.

```text
cd path/to/solution
dotnet new rehost-webforms --webapp Shop.Web
```

`--webapp` is the path to the legacy folder from where you stand. Its last
segment names the new projects and the application assembly, so `Shop.Web` gives:

```text
Shop.Web/                  legacy application, untouched
Shop.Web.App/              compiles the legacy *.cs into Shop.Web.dll
Shop.Web.Host/             the process that replaces IIS
Shop.Web.Rehost.slnx
```

`Web.config` often names the application assembly (`type="…, Shop.Web"`), so the
new assembly must keep the old name. If the legacy assembly name is not the
folder name, say so:

```text
dotnet new rehost-webforms --webapp Web --assembly Shop.Web
```

## 3. Carry your packages over

Open `Shop.Web.App/Shop.Web.App.csproj` and add a `PackageReference` for each
line of the legacy `packages.config`. Packages that are built on `System.Web` have
a Rehost counterpart. Use it, not the original: the original compiles, then fails
at run time because it binds to Microsoft's `System.Web`.

| Legacy package | Use |
| --- | --- |
| `Microsoft.AspNet.FriendlyUrls` | `Rehost.WebForms.FriendlyUrls` |
| `Microsoft.AspNet.Web.Optimization` | `Rehost.WebForms.Optimization` |
| `Microsoft.AspNet.Web.Optimization.WebForms` | `Rehost.WebForms.Optimization.WebForms` |
| `Microsoft.AspNet.ScriptManager.*` | `Rehost.WebForms.ScriptManager.Bundles` |
| `Microsoft.Owin.Host.SystemWeb` | `Rehost.WebForms.Owin.Host.SystemWeb` |
| `Microsoft.AspNet.WebApi` and `.WebHost` | `Rehost.AspNet.WebApi.WebHost` (brings `Core` and `Client`) |

The template already lists the first four, because the Visual Studio template
adds them to every project. Delete the ones your application does not use.
Other packages (Entity Framework 6, Autofac, log4net, Newtonsoft.Json) are
referenced as they are; `NU1701` is silenced because many ship only a .NET
Framework build.

A Rehost package depends on the pure-managed siblings its original depended
on, so their `packages.config` lines can go: `Microsoft.AspNet.WebApi.Core` and
`.Client` come with the Web API host, `Microsoft.Owin` and `Owin` with the OWIN
host, `WebGrease`, `Antlr` and `Newtonsoft.Json` with Optimization. Keeping the
line is harmless, it only repeats a transitive reference, unless it pins an
older version than the Rehost package asks for, in which case NuGet takes the
newer one anyway.

## 4. Adjust configuration

`Shop.Web.Host/Web.Rehost.config` is an XDT transform, the same format as
`Web.Release.config`. The build applies it to the legacy `Web.config` and writes
the result to `Shop.Web.Host/rehost_root/web.config`. The legacy file is never
changed.

Keep the three adjustments the file starts with. Add your own below them:
connection strings for this environment, and removal of modules whose assembly
cannot load on .NET.

## 5. Run

```text
dotnet run --project Shop.Web.Host
```

The URL comes from `Shop.Web.Host/Properties/launchSettings.json`. To use another
one, add `-- --urls http://127.0.0.1:5090`. An edit to an `.aspx` file needs a
rebuild, because the Host serves a copy under `rehost_root/`.

What to expect on the Visual Studio template: `GET /` returns 200 and renders the
home page; its form posts back to `./`; a `POST /` carrying the rendered
`__VIEWSTATE`, `__VIEWSTATEGENERATOR` and `__EVENTVALIDATION` fields returns 200
and renders the same page again. `/Default.aspx` answers 301 to `/Default`
(Friendly URLs). `apps/WebFormsApplication/smoke.sh <url>` in the repository
checks these rows and the script bundles.

Name the project or the `.slnx` file in every command. The folder also holds the
legacy `.sln`, and a bare `dotnet build` stops with MSB1011.

## 6. Publish

```text
dotnet publish Shop.Web.Host -c Release -o site
```

`site/` is the site root and `site/bin/` holds the binaries. Publish applies
`Web.Release.config` first and `Web.Rehost.config` second. Start it with
`dotnet site/bin/Shop.Web.Host.dll --urls http://0.0.0.0:8080`;
`launchSettings.json` is a development file and is not published.

## What the files are

| File | Purpose |
| --- | --- |
| `Shop.Web.App.csproj` | `RehostAppContentRoot` points at the legacy folder; every `*.cs` under it compiles into the application assembly. `RehostAppContentExcludes` leaves files out. |
| `Shop.Web.Host.csproj` | `RehostSiteContentRoot` points at the same folder; pages and content are copied to `rehost_root/`. `OutDir` sends the binaries to `rehost_root/bin/`. |
| `Program.cs` | The ASP.NET Core host. `AddRehostWebForms` and `UseRehostWebForms` are the two calls. |
| `Web.Rehost.config` | The transform from step 4. |
| `Properties/launchSettings.json` | The development URL. |
| `.gitignore` | Keeps `rehost_root/` out of source control; it is build output. |

## When the build fails

- **A type from `Microsoft.AspNet.*` or `Microsoft.Owin.Host.SystemWeb` is
  missing.** Add the Rehost package from the table, not the original.
- **CS0246 in a file that the legacy project does not compile.** The App project
  takes every `*.cs` on disk. List dead files in `RehostAppContentExcludes`.
- **A Windows-only API** (registry, COM, DPAPI). See
  [compatibility](compatibility.md) for what is in and out of contract.

Next: [migrating an application](migration.md) covers machine keys, generated
output, the base directory and the root configuration files.
