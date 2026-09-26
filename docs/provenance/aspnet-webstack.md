# ASP.NET Web Stack source

`Rehost.AspNet.WebApi.WebHost` is built from the archived
[`aspnet/AspNetWebStack`](https://github.com/aspnet/AspNetWebStack) repository
at tag `v3.3.0`, revision `1231b77d79956152831b75ad7f094f844251b97f`. The
sibling checkout `../AspNetWebStack` is never a build input.

The package takes `src/System.Web.Http.WebHost` (tree
`835324b71109144dade2b88c919433409dbd6699`) without its `.csproj`,
`packages.config` and `Properties/AssemblyInfo.cs`, and the seven `src/Common`
files that project links in, under `Common/`. `src/CommonAssemblyInfo.cs` is
not taken: the assembly name and version are the package's own. Both `.resx`
files keep upstream's logical resource names, and `ASPNETMVC` is defined as
upstream's build defines it. Files are byte copies except one edit.

`HttpControllerRouteHandler._instance` loses its `readonly` modifier. The
session-enabled route-handler recipe replaces that field by reflection from
`Application_Start`; Framework permitted writing an initialized static
`initonly` field, and modern .NET throws `FieldAccessException` instead.

Every source header declares Apache-2.0; upstream's `LICENSE.txt` is preserved
at `third_party/aspnet/AspNetWebStack/LICENSE.txt`.
