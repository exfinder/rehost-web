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
| `YAF.Types/`, `YAF.Configuration/`, `YAF.Core/`, `YAF.UrlRewriter/`, `YAF.Web/` | One sidecar per YAF library, each keeping its upstream assembly name. |
| `YAF.Data.PostgreSQL/`, `YAF.Data.SqlServer/` | The two database backends. `YAF.App` references the PostgreSQL one; both build. |
| `ServiceStack.OrmLite/`, `.PostgreSQL/`, `.SqlServer/` | Sidecars over the vendored OrmLite fork and its two dialects. |
| `System.Web.Http.WebHost/` | Recompile of Web API's host, byte-identical upstream source plus one field. |
| `YAF.Compat/` | Five shims for APIs modern .NET dropped, so the frozen sources need no edit. |
| `YAF.Host/` | The process: a ~20-line Kestrel host and the app's `Web.Rehost.config`. |
| `Sidecar.props` | Settings every frozen sidecar shares, including the pinned language version. |

The sidecars glob the frozen tree rather than using `RehostAppContentRoot`: four orphaned
code-behind files sit on disk that the frozen project never compiles, whose markup is gone
and whose namespace no longer exists.

## Commands

```text
dotnet build apps/YAF/YAF.slnx
dotnet run --project apps/YAF/YAF.Host
# http://127.0.0.1:5087/ (pass a URL as the first argument to change)

apps/YAF/smoke.sh                     # against the default URL
eng/app-linux-smoke.sh YAF 5087       # the same, in a Linux container
```

PostgreSQL, isolated from the other applications' instances. It is native on arm64 and
ready in about two seconds, where the SQL Server image runs emulated and took ten to
twenty-five, crashing twice during this bring-up:

```text
docker run -d --name rehost-yaf-pg -e POSTGRES_USER=yaf \
  -e POSTGRES_PASSWORD='Rehost!Dev2026' -e POSTGRES_DB=yafnet \
  -p 15432:5432 postgres:17-alpine
```

## Switching to SQL Server

Both data assemblies register `DbProviderFactory` unnamed in their Autofac module, so only
one may be loaded, which is why upstream ships a separate website project per database.
Point `YAF.App` at `../YAF.Data.SqlServer/YAF.Data.SqlServer.csproj` and change the
`yafnet` entry in `Web.Rehost.config` to the SQL Server connection string with
`providerName="Microsoft SQL Server"`. Nothing else differs; the journey was green on
SQL Server on all three platforms before PostgreSQL became the default.

Then browse to `/`, which redirects to `/install/default.aspx` against an empty database,
and walk YAF's own wizard: Next, Next, Next, Next, Initialize Database, then a board named
`Rehost Test Forum` with super user `hostadmin` / `Rehost!Dev2026` and base URL mask
`http://127.0.0.1:5087/`. The wizard is a deployment step, not part of the journey.

## Dependencies

| Upstream | Here | Note |
| --- | --- | --- |
| Six YAF libraries and the website | sidecars, same assembly names | All bind Framework `System.Web`; binary interchangeability is unsupported |
| vendored OrmLite and its dialects | sidecars | `NETFX;NET481` from the frozen `ServiceStack/Directory.Build.props` must be repeated, or `PclExport.Instance` is null and every query dies in `Env`'s static constructor |
| vendored Lucene, five projects | `Lucene.Net` 4.8.0-beta00018 packages | The tree vendors Lucene.NET renamed to `YAF.Lucene.Net`, which exists to avoid an identity clash under DNN hosting. Only `Services/Search.cs` names those types, so the packages replace 26 MB of source for ten `using` lines |
| `Microsoft.AspNet.WebApi.Core` 5.3.0 | same package, unchanged | References no `System.Web` at all |
| `Microsoft.AspNet.WebApi.WebHost` 5.3.0 | `System.Web.Http.WebHost/` | The one Framework-bound Web API assembly, and `Application_Start` reaches it |
| `Microsoft.Owin.Host.SystemWeb` 4.2.3 | `Rehost.WebForms.Owin.Host.SystemWeb` | The documented substitution |
| `Microsoft.Owin.*`, `Microsoft.AspNet.Identity.*` | same packages, unchanged | Cookie sign-in and the Identity stores are exercised |
| `OEmbed.Core` 2.0.7 | same package, `net10.0` asset | The package ships a different contract per target: `net481` has the sync `Embed`, `net10.0` only `EmbedAsync`. Taking the modern asset moves one call site to the async method |
| `Autofac` 9.3.2, `Newtonsoft.Json`, `Farsi.Library` | same packages, unchanged | Autofac is activation-critical |
| `Npgsql` 8.0.9 | same package, unchanged | The data provider the default backend uses; `providerName` in the connection string is what selects the backend |
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

`YAF.Host/Web.Rehost.config` replaces the package default wholesale, so it
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
member's reply so a guest no longer sees it. Then the two services behind those pages:
`/api/Forum/GetForums` through the recompiled Web API host, and
`/api/Search/GetSearchResults` finding the topic this run posted, which is what proves the
Lucene packages index and query.

Fifty-nine checks. The journey is repeatable against a board it has already run on: it
asserts on what it creates, not on install-seeded content that later posts push out of
view.

## Verified by hand, not by `smoke.sh`

Process restart: with the host replaced and the same cookie jar, `/` still answers 200,
the session is still `hostadmin` with administration visible, and topics and messages
persist in SQL. `smoke.sh` speaks only to a URL, so it cannot replace the process the
Linux runner owns.

## Not exercised

Image resizing and avatars, attachments, private messages, multi-board creation,
virtual-directory hosting, the upgrade path, and mail delivery over a network. Of the ten
Web API controllers only Forum and Search are reached; search is exercised through its API
rather than the search page's own JavaScript. Registration is the only mail the journey sends, and it lands in a
directory rather than on a wire.
