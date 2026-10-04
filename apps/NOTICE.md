# Sample application notices

Every directory here is a third-party application kept to demonstrate
migration. Each keeps its upstream license; the repository `LICENSE` (MIT)
covers only the sidecar projects, shims and scripts this project wrote.
Modified copies of imported files keep their upstream terms.
Source revisions and licenses are in `docs/dev/sources.md`.

| App | Upstream | License | Third-party parts inside | Gaps |
| --- | --- | --- | --- | --- |
| `WebFormsApplication` | Visual Studio 2013+ Web Forms template output | Microsoft template code; no license file | bootstrap 5.2.3 (MIT), jQuery 3.7.0 (MIT), Modernizr 2.8.3 (MIT/BSD), Microsoft `Scripts/WebForms/**` and `MSAjax/**` (see below) | No template version or hash recorded |
| `WebFormsIdentityApplication` | Visual Studio 2022 Web Forms template output | Same as above | Same set as above | Same as above |
| `WingtipToys` | MSDN Code Gallery `Getting-Started-with-221c01f5`, archive SHA-256 in its README | Apache-2.0 per the package `license.rtf` | jQuery 1.10.2 (MIT), bootswatch 3.2.0 (MIT), Modernizr 2.6.2, Respond 1.2.0 (MIT/BSD), Glyphicons Halflings fonts | `license.rtf` not imported; Glyphicons fonts carry no license text |
| `eShopLegacyWebForms` | `dotnet-architecture/eShopModernizing` `63bc9ec` | MIT (repository license) | jQuery 3.3.1, bootstrap 4.3.1, popper.js 1.14.3 (all MIT), Microsoft `Scripts/WebForms/**` (see below); `Autofac.Integration.Web` recompile of Autofac.Web 4.0.0 (MIT headers) | Autofac.Web source revision not recorded |
| `eShopLegacyMVC` | `dotnet-architecture/eShopModernizing` `63bc9ec` | MIT (repository license) | jQuery 3.3.1, jQuery Validation 1.17.0, bootstrap 4.3.1, popper.js 1.14.3 (all MIT), Modernizr 2.6.2 and 2.8.3 and Respond (MIT/BSD), jQuery Validation Unobtrusive 3.2.11 (Apache-2.0), Montserrat 2.001 fonts (OFL-1.1, `fonts/OFL.txt`); `eShopLegacy.Utilities` from the same solution; `Autofac.Integration.Mvc` recompile of Autofac.Mvc `v4.0.2` (MIT) | None recorded |
| `AjaxControlToolkitSampleSite` | `DevExpress/AjaxControlToolkit` archived v20.1, `0769b45` | BSD-3-Clause, `AjaxControlToolkit/LICENSE.txt` | Toolkit scripts and images (BSD-3-Clause) | Sample site and Sanitizer folders have no license file of their own |
| `YAF` | `YAFNET/YAFNET` v3.2.16 and `aspnet/AspNetWebStack` v3.3.0 | Apache-2.0 (headers; upstream `LICENSE.md`) | ServiceStack.OrmLite fork (Apache-2.0 per fork headers), YAF.UrlRewriter (MIT), bootstrap 5.3.8 and bootswatch themes (MIT), flag-icons (MIT), Font Awesome Free (CC BY 4.0 / OFL 1.1 / MIT), `color-modes.js` (CC BY 3.0), SCEditor, Choices.js, PrismJS, CodeMirror | Upstream `LICENSE.md` not imported; SCEditor, Choices.js, PrismJS, CodeMirror and four bootswatch 5.3.8 themes carry no license text in the tree |
| `BlogEngine` | `BlogEngine/BlogEngine.NET` v3.3.8.0, `e81ba74`: the `BlogEngine.Core` and `BlogEngine.NET` folders, `setup/` without its MySQL, SQL Server, SQL CE, Mono and test-file folders or the Windows SQLite DLL, and two `lib/` binaries | MS-RL, stated in the upstream README on `master` ("Code released under the MS-RL License", added in `43d25d8`, 2023); the v3.3.8.0 tree carries no license statement. The modified copies under `BlogEngine.Core/Packaging` and `BlogEngine.Core/Providers` stay under MS-RL | jQuery 1.9.1, 2.1.4 and 3.2.1, jQuery UI 1.10.0, Bootstrap 3.3.5 and 4.0.0, AngularJS 1.3.0, Summernote 0.6.10, toastr, Moment.js 2.10.6, popper.js, perfect-scrollbar 1.2.0, TextExt 1.3.1 (all MIT), Font Awesome 4.4.0 (OFL 1.1 / MIT), TinyMCE 4.2.4 (LGPL-2.1, `admin/editors/tinymce/license.txt`), jTemplates 0.8.4, jQuery Form and MediaElement.js (MIT / GPL), json2 (public domain), SyntaxHighlighter 3.0.83 and XRegExp 1.5.1 (MIT); `lib/` AjaxMin 4.6 (Apache-2.0), BlogML 2.5 (BSD-3-Clause); SharpZipLib 1.4.2 restores from NuGet (MIT) | MS-RL text and dependency notices accompany the site; [license evidence](BlogEngine/BlogEngine/BlogEngine.NET/licenses/README.md). The MS-RL attribution relies on the later upstream README |

## Montserrat fonts in eShop MVC

The fonts' own name tables declare version 2.001 and OFL-1.1.
[`fonts/OFL.txt`](eShopLegacyMVC/eShopLegacyMVC/fonts/OFL.txt) retains their
embedded copyright and Reserved Font Name. Its license body is copied from
the upstream [2.001 branch's OFL text](https://github.com/JulietaUla/Montserrat/blob/b344ef6134ff331f62d4cb54c22f4bb5bea2f432/OFL.txt).
The ten font files remain unchanged copies of the pinned eShop sources.

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
