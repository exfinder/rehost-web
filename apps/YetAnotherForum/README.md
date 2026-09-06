# YetAnotherForum.NET

YAF.NET 3.2.16 — a .NET Framework 4.8.1 forum with fourteen source projects, ASP.NET
Identity over OWIN cookies, a custom URL rewriter, three managed handlers, a code-first
migration engine over a vendored ServiceStack OrmLite, vendored Lucene, and Web API —
running on the ported runtime from packages, against SQL Server.

Nothing here is a support claim; [`docs/compatibility.md`](../../docs/compatibility.md)
remains the only one. The pre-import analysis is
[`docs/research/yaf-portability.md`](../../docs/research/yaf-portability.md); what the
import actually changed is [`docs/provenance/yafnet.md`](../../docs/provenance/yafnet.md).

## Provenance

| Property | Value |
| --- | --- |
| Upstream | `YAFNET/YAFNET`, tag `v3.2.16`, `339f1c15cad71bfcca7d12b44a9ead48563f410e` |
| Imported | `yafsrc` tree `31e836d7aa1d6886821160162f83aa95e57c7451`, 2026-09-05 |
| Web API host | `aspnet/AspNetWebStack`, tag `v3.3.0`, `1231b77d79956152831b75ad7f094f844251b97f` |

The frozen tree keeps upstream bytes: `.gitattributes` exempts it from line-ending
normalisation. Import exclusions and every source deviation are in the provenance record.

## Layout

| Folder | Role |
| --- | --- |
| `yafsrc/` | The frozen 4.8.1 sources, minus the three non-SQL-Server database variants. Never modified except as the provenance record lists. |
| `YAF.App/` | The website assembly, `YAF.dll`, from `yafsrc/YetAnotherForum.NET/**/*.cs`. |
| `YAF.Types/`, `YAF.Configuration/`, `YAF.Core/`, `YAF.UrlRewriter/`, `YAF.Web/`, `YAF.Data.SqlServer/` | One sidecar per YAF library, each keeping its upstream assembly name. |
| `ServiceStack.OrmLite/`, `ServiceStack.OrmLite.SqlServer/` | Sidecars over the vendored OrmLite fork. |
| `YAF.Lucene.Net*/` | Sidecars over the five vendored Lucene projects. |
| `System.Web.Http.WebHost/` | Recompile of Web API's host, byte-identical upstream source plus one field. |
| `YAF.Compat/` | Five shims for APIs modern .NET dropped, so the frozen sources need no edit. |
| `YetAnotherForum.Host/` | The process: a ~20-line Kestrel host and the app's `Web.Rehost.config`. |
| `Sidecar.props` | Settings every frozen sidecar shares, including the pinned language version. |

The sidecars glob the frozen tree rather than using `RehostAppContentRoot`: four orphaned
code-behind files sit on disk that the frozen project never compiles, whose markup is gone
and whose namespace no longer exists.

## Commands

```text
dotnet build apps/YetAnotherForum/YetAnotherForum.slnx
dotnet run --project apps/YetAnotherForum/YetAnotherForum.Host
# http://127.0.0.1:5087/ (pass a URL as the first argument to change)

apps/YetAnotherForum/smoke.sh                     # against the default URL
eng/app-linux-smoke.sh YetAnotherForum 5087       # the same, in a Linux container
```

SQL Server, isolated from the other applications' instances:

```text
docker run -d --name rehost-yaf-sql -e ACCEPT_EULA=Y \
  -e 'MSSQL_SA_PASSWORD=Rehost!Dev2026' -p 14334:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
docker exec rehost-yaf-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
  -P 'Rehost!Dev2026' -C -Q "CREATE DATABASE yafnet;"
```

Then browse to `/`, which redirects to `/install/default.aspx` against an empty database,
and walk YAF's own wizard: Next, Next, Next, Next, Initialize Database, then a board named
`Rehost Test Forum` with super user `hostadmin` / `Rehost!Dev2026` and base URL mask
`http://127.0.0.1:5087/`. The wizard is a deployment step, not part of the journey.

## Dependencies

| Upstream | Here | Note |
| --- | --- | --- |
| Six YAF libraries and the website | sidecars, same assembly names | All bind Framework `System.Web`; binary interchangeability is unsupported |
| vendored OrmLite, OrmLite.SqlServer | sidecars | `NETFX;NET481` from the frozen `ServiceStack/Directory.Build.props` must be repeated, or `PclExport.Instance` is null and every query dies in `Env`'s static constructor |
| vendored Lucene, five projects | sidecars, no source change | Their own build file knows up to net9, so net10 gets none of its `FEATURE_*` defines, which is the set net481 got. Search is unexercised |
| `Microsoft.AspNet.WebApi.Core` 5.3.0 | same package, unchanged | References no `System.Web` at all |
| `Microsoft.AspNet.WebApi.WebHost` 5.3.0 | `System.Web.Http.WebHost/` | The one Framework-bound Web API assembly, and `Application_Start` reaches it |
| `Microsoft.Owin.Host.SystemWeb` 4.2.3 | `Rehost.WebForms.Owin.Host.SystemWeb` | The documented substitution |
| `Microsoft.Owin.*`, `Microsoft.AspNet.Identity.*` | same packages, unchanged | Cookie sign-in and the Identity stores are exercised |
| `OEmbed.Core` 2.0.7 | same package, `net481` asset pinned | Its `net10.0` asset drops the sync `Embed` the BBCode module calls, offering only `EmbedAsync`; the `net481` assembly binds no Framework-only identity |
| `Autofac` 9.3.2, `Newtonsoft.Json`, `Farsi.Library` | same packages, unchanged | Autofac is activation-critical |
| `System.Data.Linq` | `YAF.Compat` | One obsolete model uses one attribute |
| `System.Web.DynamicData`, `System.Web.Entity`, `System.ServiceModel`, `EnterpriseServices` | dropped | Referenced by the frozen projects, reached by nothing in the closure |
| `System.Drawing` | `System.Drawing.Common` | Compiles; image paths are unexercised and not portable off Windows |
| `System.ServiceModel.Syndication` | modern package | RSS types only |

`YAF.Compat` supplies `CallContext`, `System.Data.Linq.Mapping.TableAttribute`, an
`AppDomain.DefineDynamicAssembly` extension, and `WindowsImpersonationContext` with an
`Impersonate` that refuses. Each replaces an API modern .NET dropped, so no frozen source
changes for them.

## Language version

`Sidecar.props` pins `LangVersion` 13, and `YAF.App` alone takes 14 because one file uses
the `field` keyword. C# 14 lets implicit span conversions into overload resolution, so
`array.Contains(x)` binds to `MemoryExtensions` instead of `Enumerable`, and OrmLite then
cannot box the resulting `ReadOnlySpan` into an expression tree. Any frozen tree rebuilt
here has that exposure.

## web.config

`YetAnotherForum.Host/Web.Rehost.config` replaces the package default wholesale, so it
repeats the default's `<runtime>` removal, then: points the `yafnet` connection string at
the container, inserts an explicit `<machineKey>` so the auth cookie survives a process
replacement, and removes `<system.net>`, which fails activation here and configures
nothing on modern .NET either way
([reading](../../docs/follow-ups/system-net-mail-settings.md)).

`App_Browsers/` is excluded from the staged site by `RehostSiteContentExcludes`:
application browser compilation is Unsupported and fails activation. The rendered pages
still emit `__doPostBack`, so the markup is not downlevel.

`yafsrc/YetAnotherForum.NET/Web.config` is a byte copy of upstream's own
`recommended.web.config`. Upstream generates `web.config` at install time and git-ignores
it, so a source checkout ships none.

## Covered by `smoke.sh`

Anonymous board index, category, topic and theme stylesheet over YAF's friendly URLs; the
guest refused the post editor, sign-out and administration. The administrator signed in
through the rendered login form, setting the OWIN `.AspNet.ApplicationCookie`. A member
registered, the verification mail written to the pickup directory, its approval link
followed, and that member signed in and refused administration. The administrator opening a
topic, the member replying to it, a guest reading the reply, the member allowed to delete
their own reply but not the administrator's post, and the administrator deleting the
member's reply so a guest no longer sees it.

Fifty-four checks, all GET and POST through rendered forms and view state.

## Verified by hand, not by `smoke.sh`

Process restart: with the host replaced and the same cookie jar, `/` still answers 200,
the session is still `hostadmin` with administration visible, and topics and messages
persist in SQL. `smoke.sh` speaks only to a URL, so it cannot replace the process the
Linux runner owns.

## Not exercised

Search and indexing, image resizing and avatars, attachments, private messages, multi-board
creation, the Web API controllers, virtual-directory hosting, the upgrade path, and mail
delivery over a network. Registration is the only mail the journey sends, and it lands in a
directory rather than on a wire.
