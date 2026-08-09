# ASP.NET Web Optimization source

The import authority is the official archived
[`aspnet/AspNetWebOptimization`](https://github.com/aspnet/AspNetWebOptimization)
repository. The local sibling checkout is `../AspNetWebOptimization`, pinned at
revision `65e3911ffed89a5a24e15e593ae74fb0f4cd6cde` from 2017-09-07.

Planned imports:

| Upstream tree | Tree object | Files | Local destination |
| --- | --- | ---: | --- |
| `src/System.Web.Optimization` | `af369150b025cbcc680a8d48b03a2db7f476fbe9` | 57 | `src/System.Web.Optimization.ReferenceSource` |
| `src/WebForms` | `6fa273a0b6f02f9b984fbf8299290215ba6ae550` | 6 | `src/Microsoft.AspNet.Web.Optimization.WebForms.ReferenceSource` |

The sibling checkout is never a build input. Imported source will be committed
locally and kept unchanged where practical; Rehost projects own compatibility
replacements.

Every C# source header declares Apache-2.0. The archived repository does not
contain the `License.txt` named by those headers. Preserve the headers and add a
verified Apache-2.0 license copy when the source is imported; do not record the
import as complete before then.

The source has no P/Invoke or native library declarations. Its principal legacy
dependencies are `System.Web`, `System.Configuration`,
`Microsoft.Web.Infrastructure`, Newtonsoft.Json, Antlr, and WebGrease. The
WebForms project contains one `BundleReference` control; its `System.Design`
lookup is reflective.

The broader official [`aspnet`](https://github.com/aspnet) organization is the
first source-discovery stop for other NuGet-era ASP.NET 4.x companions before
reimplementing them. It includes Web Optimization, MVC/Web API/Web Pages,
Identity, Katana/SignalR-era work, Session State, Output Cache, and configuration
builders, mixed with unrelated ASP.NET Core repositories. Authority, build
completeness, and license still require repository-by-repository checks.
