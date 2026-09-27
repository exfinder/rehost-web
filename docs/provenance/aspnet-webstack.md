# ASP.NET Web Stack source

Two packages are built from the archived
[`aspnet/AspNetWebStack`](https://github.com/aspnet/AspNetWebStack) repository
at tag `v3.3.0`, revision `1231b77d79956152831b75ad7f094f844251b97f`:
`Rehost.AspNet.WebApi.WebHost` carries `Rehost.Web.Http.WebHost`, and
`Rehost.AspNet.WebPages` carries `Rehost.Web.WebPages`,
`Rehost.Web.WebPages.Razor` and `Rehost.Web.WebPages.Deployment`. Each
assembly is named by the rule in [ADR 0009](../adr/0009-assembly-graph.md).
The sibling checkout `../AspNetWebStack` is never a build input.

| Upstream tree | Tree object | Local destination |
| --- | --- | --- |
| `src/System.Web.Http.WebHost` | `835324b71109144dade2b88c919433409dbd6699` | `src/Rehost.Web.Http.WebHost` |
| `src/System.Web.WebPages` | `d5e4ee4a0969cd041370423ebf5c9b4600473fa5` | `src/Rehost.Web.WebPages` |
| `src/System.Web.WebPages.Razor` | `1bd988705495efae335b5a7b55598f542f943557` | `src/Rehost.Web.WebPages.Razor` |
| `src/System.Web.WebPages.Deployment` | `013faea0a738ed1244f0421c08005435b199b116` | `src/Rehost.Web.WebPages.Deployment` |

Every tree is taken without its `.csproj`, `packages.config` and
`Properties/AssemblyInfo.cs`, and the `src/Common` files its project links in
come along under `Common/`: seven for the Web API host, and for Web Pages
`CollectionExtensions.cs`, `Empty.cs`, `HashCodeCombiner.cs`,
`ListWrapperCollection.cs`, `PathHelpers.cs` and `PropertyHelper.cs`. Each
Web Pages component also takes `src/CommonResources.resx` and its designer
file under `Common/`. `src/CommonAssemblyInfo.cs` and `src/GlobalSuppressions.cs`
are not taken. Every `.resx` keeps upstream's logical resource name, and
`ASPNETMVC` or `ASPNETWEBPAGES` is defined as upstream's build defines it. The
`Instrumentation/*.tt` templates come with the Web Pages tree; nothing runs
them, and the files they generated are in the tree.

The Web API host carries the package family version. The three Web Pages
assemblies carry assembly version 3.0.0.0, upstream's, with the family version
as file and informational version, because Web Pages reads its own assembly
version: the `X-AspNetWebPages-Version` header, the `webpages:Version` check at
startup and `WebPagesDeployment.GetVersion*` all report it. A local
`AssemblyInfo.cs` in each Web Pages component keeps the attributes that still
do something: `PreApplicationStartMethod`, `NeutralResourcesLanguage("en-US")`,
`CLSCompliant` and `ComVisible(false)`. Upstream's title, company, copyright
and product text, the `Serviceable` metadata, and the `InternalsVisibleTo` rows
naming `System.Web.Mvc`, `System.Web.Helpers` and upstream's test assemblies
are dropped.

Web Pages' `Microsoft.Web.Infrastructure` calls compile unchanged against
`Rehost.Web.Infrastructure` in the `Rehost.WebForms` package
([provenance](microsoft-web-infrastructure.md)), and `Microsoft.AspNet.Razor`
3.3.0 (`System.Web.Razor`) is consumed as shipped under `NU1701`.

Files are byte copies except these edits:

- `HttpControllerRouteHandler._instance` loses its `readonly` modifier. The
  session-enabled route-handler recipe replaces that field by reflection from
  `Application_Start`; Framework permitted writing an initialized static
  `initonly` field, and modern .NET throws `FieldAccessException` instead.
- `WebPagesDeployment.GetIncompatibleDependencies` checks its argument and
  then throws `PlatformNotSupportedException`: it loaded the application's
  `bin` assemblies into a second AppDomain, which .NET does not have.
  `AppDomainHelper.cs`, which did that, is not taken.
- `WebPagesDeployment.GetAssemblyPath` checks its argument and then throws
  `PlatformNotSupportedException` naming the registry key it read,
  `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\ASP.NET Web Pages\v<major>.<minor>`,
  which only a Web Pages installer on Windows writes.
- `CryptoUtil.ComputeSHA256` hashes with `SHA256.Create()` where upstream
  constructed `SHA256Cng`, which .NET supports only on Windows. The digest is
  the same.
- `HtmlHelper.Input.cs` loses the `System.Data.Linq.Binary` branch of
  `Html.Hidden` and its `using`: LINQ to SQL does not exist on .NET 10. A
  `byte[]` value is still written as base64.

Every source header declares Apache-2.0; upstream's `LICENSE.txt` is preserved
at `third_party/aspnet/AspNetWebStack/LICENSE.txt`.
