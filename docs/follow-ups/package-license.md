# Package license and redistribution inventory

Decided 2026-09-12 (#9): project-authored code is MIT, copyright Ex Finder.
Root [`LICENSE`](../../LICENSE) carries the text. Imported trees keep their
own licenses; nothing here replaces them.

## Package license expressions

`src/Directory.Build.props` sets `PackageLicenseExpression` and packs
[`THIRD-PARTY-NOTICES.txt`](../../THIRD-PARTY-NOTICES.txt) into every package.

| Package | Expression | Why |
| --- | --- | --- |
| `Rehost.WebForms` (Runtime, ApplicationServices, Extensions, WebServices) | MIT | Reference Source, dotnet/winforms and dotnet/msbuild imports are MIT; project code is MIT |
| `Rehost.WebForms.Hosting` | MIT | Project code; bundled `Microsoft.Web.XmlTransform.dll` 3.2.11 is MIT per its nuspec |
| `Rehost.WebForms.FriendlyUrls` | MIT | Project code |
| `Rehost.WebForms.ScriptManager.Bundles` | MIT | Project code |
| `Rehost.WebForms.Optimization` | Apache-2.0 | Imported AspNetWebOptimization tree; project edits offered under Apache-2.0 |
| `Rehost.WebForms.Optimization.WebForms` | Apache-2.0 | Same tree |
| `Rehost.WebForms.Owin.Host.SystemWeb` | Apache-2.0 | Imported Katana tree; project edits offered under Apache-2.0 |
| `Rehost.AspNet.WebApi.WebHost` | Apache-2.0 | Imported AspNetWebStack tree; project edits offered under Apache-2.0 |

## Evidence

| Component | Primary source read | Local copy |
| --- | --- | --- |
| Reference Source `ec9fa9ae` | upstream `LICENSE.txt` at that revision: MIT | `third_party/microsoft/referencesource/LICENSE.txt` |
| dotnet/winforms `195f89af` | `LICENSE.TXT` snapshot: MIT, .NET Foundation | `src/Rehost.WebForms.Runtime/Compatibility/Resources/WinForms195f89a/` |
| dotnet/msbuild `39950c62` | file header: MIT | `StronglyTypedResourceBuilder.cs` header only |
| AspNetWebOptimization `65e3911f` | upstream has no license file; 51 of 52 `.cs` headers say Apache-2.0 | `third_party/aspnet/AspNetWebOptimization/LICENSE.txt` (standard text) |
| AspNetKatana `v4.2.3` | upstream `LICENSE.txt` at that revision: Apache-2.0; no `NOTICE` file, so §4(d) adds nothing | `third_party/aspnet/AspNetKatana/LICENSE.txt` |
| AspNetWebStack `v3.3.0` | upstream `LICENSE.txt` at that revision: Apache-2.0; no `NOTICE` file, so §4(d) adds nothing | `third_party/aspnet/AspNetWebStack/LICENSE.txt` |
| Microsoft.Web.Xdt 3.2.11 | nuspec: `<license type="expression">MIT</license>` | none; text reproduced in the notices file |

Consumer-restored dependencies are not bundled and carry their own metadata.
WebGrease 1.6.0 and the `Microsoft.AspNet.ScriptManager.*` packages point at a
Microsoft ASP.NET component EULA whose URL no longer resolves.

Sample applications: [`apps/NOTICE.md`](../../apps/NOTICE.md).

## Status

- Payload check passed 2026-09-12 on the seven `0.1.0-alpha.1` packages the
  package tests pack: license element per the table above,
  `THIRD-PARTY-NOTICES.txt` in every package, `build/tasks/` DLL covered.
  Repeat on the #16 candidate.
- Microsoft ASP.NET script files inside the sample apps: accepted as-is by
  the maintainer on 2026-09-12; marked in `apps/NOTICE.md`, never packaged.
- Sample-app provenance gaps stay listed in `apps/NOTICE.md`; they are
  disclosure, not release blockers.
- Two unused `35MSSharedLib1024.snk` files and seven YAF `.snk` files ride
  along as upstream bytes; nothing signs with them.
