# Imported sources

Update this table only when importing or upgrading source. Keep upstream license
files and notices. Current code and tests describe adaptations.

| Import path | Upstream | Pinned revision | License |
| --- | --- | --- | --- |
| `src/System.Web.ReferenceSource` | [microsoft/referencesource](https://github.com/microsoft/referencesource), `System.Web` | `ec9fa9ae770d522a5b5f0607898044b7478574a3`; SameSite configuration fixes from `e47d8c1b6482e0babe243dff2f9ece1507c814ff` | [MIT](../../third_party/microsoft/referencesource/LICENSE.txt) |
| `src/System.Web.ApplicationServices.ReferenceSource` | microsoft/referencesource, `System.Web.ApplicationServices` | `ec9fa9ae770d522a5b5f0607898044b7478574a3` | MIT |
| `src/System.Web.Services.ReferenceSource` | microsoft/referencesource, `System.Web.Services` | `ec9fa9ae770d522a5b5f0607898044b7478574a3` | MIT |
| `src/System.Web.Extensions.ReferenceSource` | microsoft/referencesource, `System.Web.Extensions` | `ec9fa9ae770d522a5b5f0607898044b7478574a3` | MIT |
| `eng/Rehost.Web.GeneratedInputs/inputs/regular-expressions.json` | microsoft/referencesource, `regcomp/RegexPreCompiler.cs` | `ec9fa9ae770d522a5b5f0607898044b7478574a3` | MIT |
| `src/Rehost.Web/Compatibility/Remoting/CallContext.cs` | microsoft/referencesource, `mscorlib/system/runtime/remoting/callcontext.cs` | `ec9fa9ae770d522a5b5f0607898044b7478574a3` | MIT |
| `src/Rehost.Web/Compatibility/Resources` (ResX types) | [dotnet/winforms](https://github.com/dotnet/winforms) | `195f89af79d550c2da1711c45c379efd63519ac1` | [MIT](../../src/Rehost.Web/Compatibility/Resources/WinForms195f89a/LICENSE.TXT) |
| `src/Rehost.Web/Compatibility/Resources/StronglyTypedResourceBuilder.cs` | [dotnet/msbuild](https://github.com/dotnet/msbuild) | `39950c6284e1fe6e68890e3655ea7704b42e6a32` | MIT; source header |
| `src/System.Web.Optimization.ReferenceSource`, `src/Microsoft.AspNet.Web.Optimization.WebForms.ReferenceSource` | [aspnet/AspNetWebOptimization](https://github.com/aspnet/AspNetWebOptimization) | `65e3911ffed89a5a24e15e593ae74fb0f4cd6cde` | [Apache-2.0](../../third_party/aspnet/AspNetWebOptimization/LICENSE.txt) |
| `src/Rehost.Owin.Host.SystemWeb` | [aspnet/AspNetKatana](https://github.com/aspnet/AspNetKatana), host and loader | `v4.2.3`, `ceea1fc0670221495b470f0b1ffcdd7526113a22` | [Apache-2.0](../../third_party/aspnet/AspNetKatana/LICENSE.txt) |
| `src/Rehost.Web.Http.WebHost`, `src/Rehost.Web.WebPages`, `.Razor`, `.Deployment` | [aspnet/AspNetWebStack](https://github.com/aspnet/AspNetWebStack) | `v3.3.0`, `1231b77d79956152831b75ad7f094f844251b97f` | [Apache-2.0](../../third_party/aspnet/AspNetWebStack/LICENSE.txt) |
| `apps/WebFormsApplication/WebFormsApplication` | Visual Studio Web Forms template | Not recorded | Microsoft template code; [app notices](../../apps/NOTICE.md) |
| `apps/WebFormsIdentityApplication/WebFormsIdentityApplication` | Visual Studio Individual User Accounts template | Not recorded | Microsoft template code; app notices |
| `apps/eShopLegacyWebForms/eShopLegacyWebForms` | [dotnet-architecture/eShopModernizing](https://github.com/dotnet-architecture/eShopModernizing) | Not recorded | MIT |
| `apps/eShopLegacyWebForms/Autofac.Integration.Web` | Autofac.Web | `4.0.0`; source revision not recorded | MIT; source headers |
| `apps/WingtipToys/WingtipToys` | [MSDN Code Gallery archive](https://web.archive.org/web/20170710030442id_/https://code.msdn.microsoft.com/Getting-Started-with-221c01f5/file/107941/11/Getting%20Started%20with%20ASP.NET%204.5%20Web%20Forms%20and%20Visual%20Studio%202013%20-%20Wingtip%20Toys.zip) | File 107941, version 11; ZIP SHA-256 `3b8760a509118992d2b8aedfb95422160262d2c8b342e2a926a3c754a4d67468` | Apache-2.0; app notices |
| `apps/AjaxControlToolkitSampleSite` (site, toolkit, sanitizer) | [DevExpress/AjaxControlToolkit](https://github.com/DevExpress/AjaxControlToolkit) | Archived v20.1, `0769b45` | BSD-3-Clause; app notices |
| `apps/YAF/yafsrc` | [YAFNET/YAFNET](https://github.com/YAFNET/YAFNET) | `v3.2.16`, `339f1c15cad71bfcca7d12b44a9ead48563f410e` | Apache-2.0 and bundled component licenses; app notices |
| `apps/BlogEngine/BlogEngine`, `apps/BlogEngine/lib` | [BlogEngine/BlogEngine.NET](https://github.com/BlogEngine/BlogEngine.NET) | `v3.3.8.0`, `e81ba7425e860c2265f66911436bd241061274f9` | MS-RL per the upstream README; app notices |
