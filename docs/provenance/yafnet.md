# YAF.NET source

The import authority is the official
[`YAFNET/YAFNET`](https://github.com/YAFNET/YAFNET) repository. The local
sibling checkout is `../YAFNET`, pinned at tag `v3.2.16`, revision
`339f1c15cad71bfcca7d12b44a9ead48563f410e`, whose `yafsrc` tree object is
`31e836d7aa1d6886821160162f83aa95e57c7451`. The sibling checkout is never a
build input.

`apps/YetAnotherForum/yafsrc` is that tree with the three non-SQL-Server
database variants removed, since only the SQL Server closure is built:

| Removed | Reason |
| --- | --- |
| `ServiceStack/ServiceStack.OrmLite.{MySql,PostgreSQL,Sqlite}` | outside the `YAF-SqlServer` project closure |
| `YAF.Data/YAF.Data.{MySql,PostgreSQL,Sqlite}` | same |
| `YetAnotherForum.NET/YAF-{MySql,PostgreSQL,Sqlite}.csproj` | same |
| `YAF.NET-{MySql,PostgreSQL,Sqlite}.slnx` | same |

`.gitattributes` exempts the tree from line-ending normalization, so a checkout
reproduces upstream bytes and the diff below stays the whole difference.

## Deviations

Nine files differ from upstream. Every one is a compile blocker on .NET 10, not
a behavior preference.

| File | Change |
| --- | --- |
| `ServiceStack/ServiceStack.OrmLite/Base/Common/MiniProfiler/Data/ProfiledProviderFactory.cs` | drops the `CreatePermission` override; `DbProviderFactory` lost it with Code Access Security |
| `YAF.Types/Extensions/EnumerableExtensions.cs` | drops `DistinctBy`, whose body is `Enumerable.DistinctBy`'s own first-key-wins filter and which is now ambiguous with it at seven call sites |
| `YAF.Web/Controls/HelpMenu.cs` | drops an unused `using System.Runtime.Remoting.Contexts`, a namespace with no modern implementation |
| `{YAF.Configuration,YAF.Core,YAF.Data.SqlServer,YAF.Types,YAF.UrlRewriter,YAF.Web}/Properties/AssemblyInfo.cs` | drops `[assembly: AssemblyKeyFile("..\\YetAnotherForum.NET.snk")]`. The literal backslash is a filename off Windows, so the compiler fails to find the key; the port does not strong-name and claims no binary identity |

## Added

`YetAnotherForum.NET/Web.config` is a byte copy of the tree's own
`recommended.web.config`. Upstream generates `web.config` at install time and
git-ignores it, so a source checkout has none, and site staging requires one.
The host's `Web.Rehost.config` transforms it; the copy itself is unedited.

## Web API host

The `Microsoft.AspNet.WebApi.WebHost` 5.3.0 assembly binds Framework's
strong-named `System.Web`, and `YAF.Core` reaches it from `Application_Start`,
so it is rebuilt from source at
`apps/YetAnotherForum/System.Web.Http.WebHost`. `Microsoft.AspNet.WebApi.Core`
5.3.0 is consumed as shipped: it references no `System.Web` at all.

Import authority is the archived
[`aspnet/AspNetWebStack`](https://github.com/aspnet/AspNetWebStack) repository
at tag `v3.3.0`, revision `1231b77d79956152831b75ad7f094f844251b97f`, whose
`src/System.Web.Http.WebHost` tree object is
`835324b71109144dade2b88c919433409dbd6699`. That tree is copied byte-for-byte
except its `.csproj` and `packages.config`; the seven `src/Common` files and
`src/CommonAssemblyInfo.cs` its project linked in come with it, under `Common/`
and at the root, matching upstream's `Link` paths. Not one source line is
edited, and `ASPNETMVC` is defined as upstream's own build defines it, which is
what selects `AssemblyVersion` 5.3.0.0.

The shipped 5.3.0 assembly reports informational version
`5.3.0-61837 (ec2f0a5af7b4dbefba38e605c9025367a15a2f0f)`, and that revision is
not public — `v3.3.0` is the nearest published source. The gap is measured
rather than assumed: decompiling both assemblies and comparing every `public`
and `protected` declaration leaves one difference, the implicit
`WebHostBufferPolicySelector()` constructor ILSpy renders for the shipped
assembly and omits for the rebuild.

Every source header declares Apache-2.0.
