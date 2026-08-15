# WebFormsApplication

The Visual Studio 2013+ Web Forms template application, running on the ported
runtime from packages — the way an external consumer would.

## Layout

| Folder | Role |
| --- | --- |
| `WebFormsApplication/` | The frozen .NET Framework 4.8.1 WAP. Never modified; stays buildable in Visual Studio on Windows. |
| `WebFormsApplication.App/` | The port of the app assembly: compiles the legacy folder's `*.cs` into `WebFormsApplication.dll`, exactly what the WAP produced in `bin/`. |
| `WebFormsApplication.Host/` | The process: a ~30-line Kestrel host. |

The mental model for a migration: **the GAC becomes the `Rehost.WebForms`
metapackage; each `packages.config` line maps to a `Rehost.*` package; the
host exe adds `Rehost.WebForms.Hosting`; the legacy folder is never touched.**

## Commands

```text
dotnet build apps/WebFormsApplication/WebFormsApplication.slnx
dotnet run --project apps/WebFormsApplication/WebFormsApplication.Host
# http://127.0.0.1:5081/Default (pass a URL as the first argument to change)

dotnet publish apps/WebFormsApplication/WebFormsApplication.Host -c Release
# deployable site: WebFormsApplication.Host/bin/Release/net10.0/site-publish/
```

One command is always enough. Building packs any changed `src/` project into
the local feed (`artifacts/apps/feed/`, shared by every app under `apps/`)
before restore resolves, so the app can never run against a stale runtime; builds that skip
restore fail loudly instead of building stale. Packages are always packed
`Release`; `-c` governs only the app and host projects.

Build stages a runnable copy of the site to
`WebFormsApplication.Host/bin/site/` (content + `bin/` payload + transformed
`web.config`) incrementally — removed sources are deleted from the stage by
manifest, never by wiping. `dotnet run` executes the staged copy. After
editing an `.aspx`, rebuild (fast, no compile) and refresh.

## web.config

The staged `web.config` is produced by XDT: the package default
`Web.Rehost.config` removes `<runtime>` and `<system.codedom>` and rewrites the
Optimization `<controls>` assembly; drop a `Web.Rehost.config` beside the Host
project (or set `RehostWebConfigTransform`) to override. `dotnet publish`
additionally applies the app's own `Web.$(Configuration).config` first,
matching Framework publish semantics — dev runs never apply Debug/Release
transforms, exactly like F5 on Framework.

## Consumer contract used here

- App project: `RehostAppContentRoot` (runtime package targets) compiles the
  legacy tree WAP-style: all `*.cs` except `App_*`, `bin`, `obj`, `packages`.
- Host project: `RehostSiteContentRoot` (hosting package targets) turns on
  staging, the XDT pipeline, the `dotnet run` redirection, and the publish
  site layout.
- `apps/LocalFeed.props` + `apps/Directory.*` are repo-internal
  freshness machinery, not part of the consumer story; a real consumer
  restores from nuget.org and needs none of it.
