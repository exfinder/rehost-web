# Sample application notices

Every directory here is a third-party application kept to demonstrate
migration. Each keeps its upstream license; the repository `LICENSE` (MIT)
covers only the sidecar projects, shims and scripts this project wrote.
Provenance detail lives in each app's `README.md` and `docs/provenance/`.

| App | Upstream | License | Third-party parts inside | Gaps |
| --- | --- | --- | --- | --- |
| `WebFormsApplication` | Visual Studio 2013+ Web Forms template output | Microsoft template code; no license file | bootstrap 5.2.3 (MIT), jQuery 3.7.0 (MIT), Modernizr 2.8.3 (MIT/BSD), Microsoft `Scripts/WebForms/**` and `MSAjax/**` (see below) | No template version or hash recorded |
| `WebFormsIdentityApplication` | Visual Studio 2022 Web Forms template output | Same as above | Same set as above | Same as above |
| `WingtipToys` | MSDN Code Gallery `Getting-Started-with-221c01f5`, archive SHA-256 in its README | Apache-2.0 per the package `license.rtf` | jQuery 1.10.2 (MIT), bootswatch 3.2.0 (MIT), Modernizr 2.6.2, Respond 1.2.0 (MIT/BSD), Glyphicons Halflings fonts | `license.rtf` not imported; Glyphicons fonts carry no license text |
| `eShopLegacyWebForms` | `dotnet-architecture/eShopModernizing` | MIT (repository license) | jQuery 3.3.1, bootstrap 4.3.1, popper.js 1.14.3 (all MIT), Microsoft `Scripts/WebForms/**` (see below); `Autofac.Integration.Web` recompile of Autofac.Web 4.0.0 (MIT headers) | Upstream commit not recorded; Autofac.Web source revision not recorded |
| `AjaxControlToolkitSampleSite` | `DevExpress/AjaxControlToolkit` archived v20.1, `0769b45` | BSD-3-Clause, `AjaxControlToolkit/LICENSE.txt` | Toolkit scripts and images (BSD-3-Clause) | Sample site and Sanitizer folders have no license file of their own |
| `YAF` | `YAFNET/YAFNET` v3.2.16 and `aspnet/AspNetWebStack` v3.3.0 | Apache-2.0 (headers; upstream `LICENSE.md`) | ServiceStack.OrmLite fork (Apache-2.0 per fork headers), YAF.UrlRewriter (MIT), bootstrap 5.3.8 and bootswatch themes (MIT), flag-icons (MIT), Font Awesome Free (CC BY 4.0 / OFL 1.1 / MIT), `color-modes.js` (CC BY 3.0), SCEditor, Choices.js, PrismJS, CodeMirror | Upstream `LICENSE.md` not imported; SCEditor, Choices.js, PrismJS, CodeMirror and four bootswatch 5.3.8 themes carry no license text in the tree |

## Microsoft ASP.NET script files

`Scripts/WebForms/**` and `Scripts/WebForms/MSAjax/**` in the template apps and
eShop come from the `Microsoft.AspNet.ScriptManager.WebForms` and
`Microsoft.AspNet.ScriptManager.MSAjax` NuGet packages. Their headers say only
"Copyright (C) Microsoft Corporation. All rights reserved." and nuget.org
points at a Microsoft EULA whose URL no longer resolves. They are kept as part
of the sample applications they shipped with and are not offered under this
repository's MIT license. The maintainer accepted this on 2026-09-12 (#9):
they travel only inside the sample apps, never in a package. The same
scripts exist under MIT in Microsoft Reference Source; the Rehost packages
ship that MIT build.
