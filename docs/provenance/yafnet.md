# YAF.NET source

The import authority is the official
[`YAFNET/YAFNET`](https://github.com/YAFNET/YAFNET) repository. The local
sibling checkout is `../YAFNET`, pinned at tag `v3.2.16`, revision
`339f1c15cad71bfcca7d12b44a9ead48563f410e`, whose `yafsrc` tree object is
`31e836d7aa1d6886821160162f83aa95e57c7451`. The sibling checkout is never a
build input.

`apps/YAF/yafsrc` is that tree with the three non-SQL-Server
database variants removed, since only the SQL Server closure is built:

| Removed | Reason |
| --- | --- |
| `ServiceStack/ServiceStack.OrmLite.{MySql,Sqlite}` | outside the closure of either database the port builds |
| `YAF.Data/YAF.Data.{MySql,Sqlite}` | same |
| `YetAnotherForum.NET/YAF-{MySql,PostgreSQL,Sqlite}.csproj` | same |
| `YAF.NET-{MySql,PostgreSQL,Sqlite}.slnx` | same |
| `Lucene.Net/` (five projects, 26 MB) | the published `Lucene.Net` 4.8.0-beta00018 packages carry the same 4.8.0 code the fork is based on, and the rename to `YAF.Lucene.Net` exists to avoid an identity clash when YAF is hosted inside DNN, which does not apply here. `yafsrc/YAF.NET-SqlServer.slnx` still lists them and no longer opens as upstream ships it |

`.gitattributes` exempts the tree from line-ending normalization, so a checkout
reproduces upstream bytes and the diff below stays the whole difference.

## Deviations

Sixteen files differ from upstream. Nine are compile blockers on .NET 10, three
are runtime blockers measured against a running application, two follow from
choosing a dependency's published package over the copy vendored into the tree,
and one is an upstream defect that has nothing to do with the port. Each is
marked.

| File | Change |
| --- | --- |
| `ServiceStack/ServiceStack.OrmLite/Base/Common/MiniProfiler/Data/ProfiledProviderFactory.cs` | drops the `CreatePermission` override; `DbProviderFactory` lost it with Code Access Security |
| `YAF.Types/Extensions/EnumerableExtensions.cs` | drops `DistinctBy`, whose body is `Enumerable.DistinctBy`'s own first-key-wins filter and which is now ambiguous with it at seven call sites |
| `YAF.Web/Controls/HelpMenu.cs` | drops an unused `using System.Runtime.Remoting.Contexts`, a namespace with no modern implementation |
| `{YAF.Configuration,YAF.Core,YAF.Data.PostgreSQL,YAF.Data.SqlServer,YAF.Types,YAF.UrlRewriter,YAF.Web}/Properties/AssemblyInfo.cs` | drops `[assembly: AssemblyKeyFile("..\\YetAnotherForum.NET.snk")]`. The literal backslash is a filename off Windows, so the compiler fails to find the key; the port does not strong-name and claims no binary identity |
| `ServiceStack/ServiceStack.OrmLite/Base/Text/ReflectionOptimizer.Emit.cs` | `AssemblyBuilderAccess.RunAndSave` becomes `Run`. Modern .NET cannot persist a dynamic assembly and dropped the member; nothing here saves the one it builds. Reached only once `NETFX` is defined, which is what compiles this file |
| `YAF.Core/Services/MailService.cs` | gives `new SmtpClient()` a delivery method and a pickup directory under `App_Data/mail`. Framework's parameterless constructor configured itself from `<system.net><mailSettings>`; modern .NET deleted that reading, so the client has no host and every send throws "The SMTP host was not specified" ([reading](../follow-ups/system-net-mail-settings.md)). Every YAF user-creation path sends a verification mail, so without this no second account can be created. The directory mode is one of the two upstream `recommended.web.config` offers and needs no network |
| `YAF.Core/Services/Search.cs` | ten `using YAF.Lucene.Net.*` become `using Lucene.Net.*`. The tree vendors Lucene.NET under a renamed namespace; the port consumes the published `Lucene.Net` 4.8.0-beta00018 packages instead, and this is the only file that names those types. Measured: `/api/Search/GetSearchResults` returns hits with `<mark>` highlighting, so index, query and Highlighter all work through the packages |
| `YAF.Web/BBCodes/MediaBBCodeModule.cs` | calls `EmbedAsync` instead of `Embed`. `OEmbed.Core` 2.0.7 ships a different contract per target and its `net10.0` asset offers only the async one. The port takes .NET 10 assets wherever a package has them, so the call site follows. It runs through `Task.Run`, not a bare `GetAwaiter().GetResult()`: the assembly contains no `ConfigureAwait(false)`, so its continuations capture the Web Forms synchronization context a direct block would be holding. Unexercised: no journey renders a media BBCode |
| `YAF.Core/Migrations/Migration01.cs` | moves the five `CreateTable<AspNet*>` calls above the MySQL collation block. **Upstream defect, not a portability change.** Commit `7f3234f6a8` ("[FIXED] install for mysql", 2026-05-14) inserted `if (SQLServerName() != "MySQL") return;` above them, so from v3.2.14 onward a fresh install on SQL Server, PostgreSQL or SQLite creates 54 tables and none of the five ASP.NET Identity ones, and board creation then fails on `Invalid object name 'yaf_AspNetUsers'`. The reorder restores the pre-`7f3234f6a8` outcome for every provider and leaves the MySQL statements exactly where that commit put them |
| `YAF.Core/Tasks/IntermittentBackgroundTask.cs` | drops the `WindowsIdentity.GetCurrent()` capture. It throws `PlatformNotSupportedException` off Windows, and the timer callback it feeds cannot impersonate on any platform, since modern .NET replaced scoped impersonation with the callback-shaped `RunImpersonated`. One process under one identity is the hosting model, so the callback already runs as the identity the capture existed to restore. Measured: without this the first request dies in `Session_Start` |

## Language version

`Sidecar.props` pins `LangVersion` 13 for every frozen project except
`YAF.App`, whose `Controls/ForumList.ascx.cs` needs C# 14's `field` keyword.

This is not a style preference. C# 14 admits implicit span conversions into
overload resolution, so `roles.Contains(r.Id)` over a `string[]` in
`YAF.Core/Identity/UserStore.cs` binds to `MemoryExtensions.Contains` instead
of `Enumerable.Contains`. Inside an OrmLite expression tree that yields a
`ReadOnlySpan<string>` node, `CachedExpressionCompiler.Wrap` boxes it, and the
resulting IL is rejected: `InvalidProgramException` out of
`Expression<T>.Compile()`, which surfaces as "Common Language Runtime detected
an invalid program" when the installer creates its first board. net481 had no
such overload, so 13 is the newest version that reads these sources the way
their own build did. Any frozen tree recompiled here has the same exposure
wherever an array's `Contains` reaches an expression tree.

## Added

`YetAnotherForum.NET/Web.config` is a byte copy of the tree's own
`recommended.web.config`. Upstream generates `web.config` at install time and
git-ignores it, so a source checkout has none, and site staging requires one.
The host's `Web.Rehost.config` transforms it; the copy itself is unedited.

## Web API host

The `Microsoft.AspNet.WebApi.WebHost` 5.3.0 assembly binds Framework's
strong-named `System.Web`, and `YAF.Core` reaches it from `Application_Start`,
so it is rebuilt from source at
`apps/YAF/System.Web.Http.WebHost`. `Microsoft.AspNet.WebApi.Core`
5.3.0 is consumed as shipped: it references no `System.Web` at all.

Import authority is the archived
[`aspnet/AspNetWebStack`](https://github.com/aspnet/AspNetWebStack) repository
at tag `v3.3.0`, revision `1231b77d79956152831b75ad7f094f844251b97f`, whose
`src/System.Web.Http.WebHost` tree object is
`835324b71109144dade2b88c919433409dbd6699`. That tree is copied byte-for-byte
except its `.csproj`, `packages.config`, and one field in
`HttpControllerRouteHandler.cs`; the seven `src/Common` files and
`src/CommonAssemblyInfo.cs` its project linked in come with it, under `Common/`
and at the root, matching upstream's `Link` paths. `ASPNETMVC` is defined as upstream's own build defines it, which is what selects
`AssemblyVersion` 5.3.0.0.

The one edit drops `readonly` from the private static `_instance` field.
`WebApiConfig.Register` reaches into that field by reflection to install YAF's
session-enabled route handler, which is the whole reason YAF's Web API
controllers see session state. Framework's reflection permitted writing a
static initonly field; .NET Core forbids it once the type is initialized, so
the call raises `FieldAccessException` and takes `Application_Start` with it.
Dropping the modifier reproduces the Framework outcome and changes nothing for
any caller that does not reflect.

The shipped 5.3.0 assembly reports informational version
`5.3.0-61837 (ec2f0a5af7b4dbefba38e605c9025367a15a2f0f)`, and that revision is
not public, so `v3.3.0` is the nearest published source. The gap is measured
rather than assumed: decompiling both assemblies and comparing every `public`
and `protected` declaration leaves one difference, the implicit
`WebHostBufferPolicySelector()` constructor ILSpy renders for the shipped
assembly and omits for the rebuild.

Every source header declares Apache-2.0.
