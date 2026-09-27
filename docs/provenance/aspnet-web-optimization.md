# ASP.NET Web Optimization source

The import authority is the official archived
[`aspnet/AspNetWebOptimization`](https://github.com/aspnet/AspNetWebOptimization)
repository. The local sibling checkout is `../AspNetWebOptimization`, pinned at
revision `65e3911ffed89a5a24e15e593ae74fb0f4cd6cde` from 2017-09-07.

Imports:

| Upstream tree | Tree object | Files | Local destination |
| --- | --- | ---: | --- |
| `src/System.Web.Optimization` | `af369150b025cbcc680a8d48b03a2db7f476fbe9` | 57 | `src/System.Web.Optimization.ReferenceSource` |
| `src/WebForms` | `6fa273a0b6f02f9b984fbf8299290215ba6ae550` | 6 | `src/Microsoft.AspNet.Web.Optimization.WebForms.ReferenceSource` |

The sibling checkout is never a build input. Both trees are copied byte-for-byte
to the listed local destinations. Rehost projects own compatibility replacements.

Every C# source header declares Apache-2.0. The archived repository omits the
`License.txt` named by those headers; the standard license text is preserved at
`third_party/aspnet/AspNetWebOptimization/LICENSE.txt`.

The source has no P/Invoke or native library declarations. Its principal legacy
dependencies are `System.Web`, `System.Configuration`,
`Microsoft.Web.Infrastructure`, Newtonsoft.Json, Antlr, and WebGrease. The
WebForms project contains one `BundleReference` control; its `System.Design`
lookup is reflective.

Both imported trees compile on .NET 10. The port drops one file. The 4.0-era
`AssemblyMetadataAttribute` polyfill now collides with the BCL type, and the
compiler resolves such a collision in favour of the local copy, which would hide
`[assembly: AssemblyMetadata]` from anything reading the real attribute.
`PreApplicationStartCode` compiles as imported, against
`Rehost.Web.Infrastructure` in the `Rehost.WebForms` package
([provenance](microsoft-web-infrastructure.md)).

Neither tree's `Properties/AssemblyInfo.cs` is compiled. Both pinned the
1.1.0.0 assembly, file and informational versions, and the shipped assemblies
carry the package family version from `src/Directory.Build.props` instead, so
the SDK generates the version and description attributes. A local
`AssemblyInfo.cs` in the Optimization project keeps the four attributes that
still do something: `PreApplicationStartMethod`, `NeutralResourcesLanguage`,
`CLSCompliant` and `ComVisible(false)`. The COM `Guid`, the `Serviceable`
metadata, Microsoft's title and copyright text, and two `InternalsVisibleTo`
rows naming upstream's test and WebForms assemblies are dropped; the WebForms
project compiles as `Rehost.WebForms.Optimization.WebForms` and reaches no
internals.

WebGrease 1.6.0 is consumed unchanged for the reached JS/CSS bundle paths; the
source names it as `Microsoft.Ajax.Utilities`, which ships inside it. Antlr and
Newtonsoft.Json are WebGrease's own dependencies and are named explicitly only to
raise its pins — Newtonsoft 5.0.4 carries a high-severity advisory. Removing
either reference as unused reintroduces it.

The broader official [`aspnet`](https://github.com/aspnet) organization is the
first source-discovery stop for other NuGet-era ASP.NET 4.x companions before
reimplementing them. It includes Web Optimization, MVC/Web API/Web Pages,
Identity, Katana/SignalR-era work, Session State, Output Cache, and configuration
builders, mixed with unrelated ASP.NET Core repositories. Authority, build
completeness, and license still require repository-by-repository checks.
