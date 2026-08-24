# Backslash-literal audit (imported and ported source)

Question: which `\` literals in the compiled port were meant to be a **physical**
path separator on Windows, and therefore misbehave on Linux/macOS? Everything
else is classified so nothing is left unexplained.

Date: 2026-08-16. Reading is of the working tree at `main` (`afba48b`).
Outcome: ledger P74 (group A fixed with tests; B1, B2, B3, B6, B15, B16 fixed
without; the rest recorded) and P75 (`WebProcessInformation`, decision row 3).

## Scope

Trees audited:

- `src/System.Web.ReferenceSource/**`
- `src/System.Web.Services.ReferenceSource/**`
- `src/System.Web.Optimization.ReferenceSource/**`
- `src/Rehost.WebForms.Extensions/**`, `src/Rehost.WebForms.FriendlyUrls/**`,
  `src/Rehost.WebForms.ApplicationServices/**`, `src/Rehost.WebForms.WebServices/**`,
  `src/Rehost.WebForms.Owin.Host.SystemWeb/**`,
  `src/Rehost.WebForms.Runtime/Compatibility/**`, `src/Rehost.WebForms.Hosting/**`

Adjacent trees the in-scope projects actually compile were swept too
(`System.Web.Extensions.ReferenceSource`, `System.Web.ApplicationServices.ReferenceSource`,
`Microsoft.AspNet.Web.Optimization.WebForms.ReferenceSource`) — see the appendix.

### What actually compiles

| project | source it compiles | notes |
| --- | --- | --- |
| `Rehost.WebForms.Runtime` | `../System.Web.ReferenceSource/**/*.cs` | 13 `Compile Remove` entries (see below) plus 3 files from `System.Web.Services.ReferenceSource/…/Configuration` |
| `Rehost.WebForms.WebServices` | one file: `System.Web.Services.ReferenceSource/System/Web/Services/WsiProfiles.cs` | **the rest of `System.Web.Services.ReferenceSource` is not compiled at all** |
| `Rehost.WebForms.Optimization` | `../System.Web.Optimization.ReferenceSource/**/*.cs` | removes `Properties/AssemblyMetadataAttribute.cs`, `PreApplicationStartCode.cs` |
| `Rehost.WebForms.Extensions` | an explicit file list out of `System.Web.Extensions.ReferenceSource` | `ClientServices/**` and `Compilation/**` are **not** in the list |
| `Rehost.WebForms.ApplicationServices` | `../System.Web.ApplicationServices.ReferenceSource/**/*.cs` minus `Hosting/CustomLoaderHelper.cs` | no backslash hits |
| `Rehost.WebForms.FriendlyUrls`, `Rehost.WebForms.Hosting`, `Rehost.WebForms.Owin.Host.SystemWeb` | own sources only | |

`Compile Remove` in `Rehost.WebForms.Runtime.csproj`:
`Compilation/{WebReferencesBuildProvider,WsdlBuildProvider,XsdBuildProvider,BrowserCapabilitiesCompiler}.cs`,
`Configuration/{BrowserCapabilitiesCodeGenerator,StrongNameUtility,IRemoteWebConfigurationHostServer,RemoteWebConfigurationHost,RemoteWebConfigurationHostServer,RemoteWebConfigurationHostStream}.cs`,
`InternalApis/NDP_Common/inc/StrongNameHelpers.cs`, `Management/regiisutil.cs`,
`Util/Transactions.cs`.

`NETFRAMEWORK` is never defined (the port targets `net10.0` only); `DBG` is not
defined either; `PLATFORM_UNIX`, `FEATURE_PAL` and `USE_MEMORY_CACHE` are not
defined.

### Method

A C# literal lexer (comment-aware, handling `"…"`, `@"…"`, `$@"…"`, `'…'`)
extracted every string/char literal whose **decoded** value contains a backslash,
across all `.cs` under the trees above. Preprocessor state was then re-derived per
line to flag `#if NETFRAMEWORK` / `#if DBG` regions, and every candidate physical
site was traced to its callers by hand. Separately grepped: `Path.AltDirectorySeparatorChar`,
`\u005c`/`\x5c`/`(char)92`, and `Path.DirectorySeparatorChar`/`Path.Combine` used on
*virtual* paths (the inverse defect).

### Counts

| | |
| --- | --- |
| literal occurrences the lexer flagged | 663 |
| in `obj/` (generated build intermediates, excluded) | 94 |
| `\xNNNN` / `\uNNNN` numeric escapes (lexer artefacts, not backslashes) | 265 |
| **real backslash literals, unique `file:line`** | **286** |

Group totals (unique `file:line` sites):

| group | meaning | sites |
| --- | --- | --- |
| **A** | reached physical-path defect — fix | **8** sites (2 defects) |
| **B** | unreached physical-path defect — fix if trivial, else record | **32** sites (16 rows) |
| **C** | correct by contract — keep | **195** sites |
| **D** | dead (not compiled, `#if NETFRAMEWORK`, `#if DBG`, `Compile Remove`) | **51** sites |

8 + 32 + 195 + 51 = 286.

---

## Group A — reached physical-path defects

### A1. `FileUtil.IsSuspiciousPhysicalPath` long-path fallback

| field | value |
| --- | --- |
| sites | `src/System.Web.ReferenceSource/Util/FileUtil.cs:214`, `:218`, `:225`, `:241` |
| snippet | `string slashDots = "\\..";` … `physicalPath[idx + 3] == '\\'` … `physicalPath.LastIndexOf('\\')` … `physicalPath.LastIndexOf('\\', pos-1)` |
| kind | **physical path** (segment walk over a physical string) |
| reachable | **yes.** `UserMapPath.GetPhysicalPathForPath:144` calls `IsSuspiciousPhysicalPath` for every configuration map-path; `HttpRuntime:3603` (`GetRelaxedMapPathResult`), `SimpleApplicationHost:34`, `VirtualDirectoryMapping:87/139` likewise. No feature gate. |
| off-Windows effect | The block runs only when the fast check set `pathTooLong` (path > 259 chars, or an invalid char, or a `:` past index 1). Its first test is `if (physicalPath.IndexOf('/') >= 0) return true;` — on Unix **every** physical path contains `/`, so the method returns "suspicious" immediately and the four `'\\'` sites below never execute. Result: a mapped path longer than 259 characters is unconditionally suspicious → `CheckSuspiciousPhysicalPath` throws `HttpException(404)`, where Framework would have trimmed right-to-left and often decided the path was fine. Deep application trees 404 on Linux/macOS and serve on Windows. |
| proposed fix | Under `#if !NETFRAMEWORK`: drop the `IndexOf('/')` early return (on Unix `/` is *the* separator, not a suspicious character) and replace the three `'\\'` literals with `Path.DirectorySeparatorChar`. Framework's Windows behaviour is unchanged because `Path.DirectorySeparatorChar == '\\'` there and the `'/'` test stays under `NETFRAMEWORK`. |
| red test | **Yes, cheap.** `FileUtilTests`: build a physical path under a temp root whose length exceeds 259 but which is canonical (`Path.GetFullPath(p) == p`), assert `FileUtil.IsSuspiciousPhysicalPath(p) == false`. Fails today on macOS/Linux (returns `true`), passes on Windows. A second case with an embedded `/../` segment must still return `true`. |

### A2. `SimpleWorkerRequest` composes physical paths with `'\\'`

| field | value |
| --- | --- |
| sites | `src/System.Web.ReferenceSource/Hosting/SimpleWorkerRequest.cs:162`, `:215`, `:411`, `:412` |
| snippet | `String path = _appPhysPath + _page.Replace('/', '\\');` — `mappedPath = appPath + path.Substring(_appVirtPath.Length).Replace('/', '\\');` — `if (!StringUtil.StringEndsWith(_appPhysPath, '\\')) _appPhysPath += "\\";` |
| kind | **physical path** |
| reachable | **Ambiguous — see "needs a decision".** `SimpleWorkerRequest` is *public* API in `System.Web.Hosting`, with no feature gate; an application or test harness can construct it and call `HttpRuntime.ProcessRequest`. The port's own hosting never does (`AspNetCoreWorkerRequest` is the worker request); the only in-tree construction is `IPipelineRuntime.cs:326`, which is IIS7-integrated-mode-only and unreached. |
| off-Windows effect | `GetFilePathTranslated()` returns `/app/sub\page.aspx` — one file name containing a backslash, so every file probe misses (404 / `FileNotFoundException`). `MapPath()` returns the same shape. The 5-arg ctor appends `"\\"` to an app path that already ends in `/`, producing `/app/\`; `MapPath` then strips one char and accidentally recovers, which makes the failure look intermittent. |
| proposed fix | `_page.Replace('/', Path.DirectorySeparatorChar)` at :162 and :215; `Path.DirectorySeparatorChar` at :411/:412. Windows unchanged. |
| red test | **Yes, cheap.** Construct `new SimpleWorkerRequest("/app", tempDir, "sub/page.aspx", null, writer)` and assert `GetFilePathTranslated()` equals `Path.Combine(tempDir, "sub", "page.aspx")`. Fails today off Windows. No hosting environment required for the 5-arg ctor. |

---

## Group B — unreached physical-path defects

One line each: `file:line` — gate that makes it unreached — off-Windows effect — fix.

| # | site | why unreached | effect if reached | fix |
| --- | --- | --- | --- | --- |
| B1 | `Compilation/CodeDirectoryCompiler.cs:270` (`CodegenDirInternal + "\\" + "Sources_" + name`) | only caller chain is `BuildManager.GetCodeDirectoryInformation` ← `BuildManagerHost` ← `ClientBuildManager` (CBM/designer only); the runtime App_Code path is `GetCodeDirectoryAssembly`, which does not take `generatedFilesDir` | generated `.cs` sources for App_Code land in a sibling directory literally named `codegen\Sources_App_Code`, outside the codegen dir, so `BuildResultCache:919`'s `Sources_` sweep never cleans them | `Path.DirectorySeparatorChar` (trivial) |
| B2 | `Configuration/TrustLevel.cs:87` and `:117` (`filename.LastIndexOf('\\')`) | only reached when `<trust level>` is not `Full`; the port is full-trust only (PROJECT.md) | `LastIndexOf` returns −1 → `strDir` empty → the policy file resolves relative to the CWD; `SetTrustLevel` then throws `Unable_to_get_policy_file` | `Path.DirectorySeparatorChar`, or better `Path.GetDirectoryName` (trivial) |
| B3 | `FileChangesMonitor.cs:1995`, `:1999` (`StringEndsWith(dirRoot,'\\')` / `dirRoot + "\\" + dirToListenTo`) | `ListenToSubdirectoryChanges` sits past the `IsFCNDisabled` early return at `:1952`; `CurrentAppDomainHosting` forces `FcnMode.Disabled` and `ApplicationBootstrap:378` rejects anything else | monitored subdirectory path gets a literal `\` → `Directory.Exists` false → the special-directory watch silently does nothing | `Path.DirectorySeparatorChar`; already recorded as a precondition on the configuration-reload follow-up alongside the six P71 alias checks |
| B4 | `Configuration/ExpressServerConfig.cs:372` (`physicalPath += "\\"` after a `X:` drive check) | `ServerConfig.GetInstance()` is only reached when `ServerConfig.UseServerConfig` is true, which needs `ISAPIApplicationHost` or `IsUnderIISProcess`; neither holds in the port | `physicalPath.Length == 2 && [1] == ':'` never matches off Windows, so the append is inert — the defect is only that the drive-root repair has no Unix equivalent | keep as-is (note only); the shape check is a P71 boundary |
| B5 | `Configuration/ProcessHostMapPath.cs:279` | same gate as B4 | same as B4 | note only |
| B6 | `Configuration/ProcessHostMapPath.cs:324`, `:330` (`result.Replace('/','\\')`, `result + "\\"`) | same gate as B4 | a native-IIS mapped path would be mangled into one backslash-bearing name | `Path.DirectorySeparatorChar` (trivial) if the class is ever revived |
| B7 | `Configuration/MetabaseServerConfig.cs:195`, `:201` | IIS 6 metabase; unreachable for the same reason as B4 | same as B6 | note only (metabase is out of the portable contract) |
| B8 | `Hosting/AppDomainFactory.cs:161`, `:162` | COM `IAppDomainFactory` entry point; nothing in the port calls it | app physical path loses its trailing separator normalisation | `Path.DirectorySeparatorChar` if revived |
| B9 | `Hosting/ProcessHost.cs:707`, `:708`, `:927`, `:928` | `ProcessHost` is the IIS 7 worker-process COM host; reached only through `IISMapPath`/`ProcessHostServerConfig` | same as B8 | note only |
| B10 | `Hosting/ISAPIWorkerRequest.cs:1010`, `:1011` | constructed only by `ISAPIRuntime.cs:158` | same as B8 | note only |
| B11 | `Hosting/IIS7WorkerRequest.cs:217`, `:219` | integrated-mode worker request; never constructed in the port | same as B8 | note only |
| B12 | `Management/WebEvents.cs:1843` (`_processName.LastIndexOf('\\')`) | **ambiguous** — `WebManagementEvent`'s static initialiser constructs `WebProcessInformation`, and health monitoring *is* live (`HttpRuntime.InitializeHealthMonitoring`). It is blocked one line earlier by `UnsafeNativeMethods.GetModuleFileName` (`kernel32.dll`), which cannot resolve off Windows | if the P/Invoke were replaced, the process name would keep its full path on Unix instead of being trimmed to the leaf | `Path.GetFileName(_processName)`; but the real work is replacing the two kernel32 calls with `Environment.ProcessPath` / `Environment.ProcessId` — see "needs a decision" |
| B13 | `DataAccess/SqlConnectionHelper.cs:147` (`while (partialFileName.StartsWith("\\")) …`) | `EnsureDBFile` runs only for `AttachDBFilename=|DataDirectory|…` with SQL Server user instances / LocalDb, which do not exist off Windows | Framework's own leading-separator strip only knows `\`; a Unix-shaped `\|DataDirectory\|/db.mdf` keeps its leading `/`, and `Path.Combine(dataDir, "/DB.MDF")` discards `dataDir` and returns `/DB.MDF` — a silently wrong absolute path | strip both separators (trivial), or note-only if LocalDb stays unsupported |
| B14 | `Security/AuthStoreRoleProvider.cs:560` (`_ConnectionString.Substring("msxml://".Length).Replace('/','\\')`) | `AuthorizationStoreRoleProvider` needs the AzMan COM interop assembly (`Microsoft.Interop.Security.AzRoles`), which is not present; COM is outside the portable contract | the `msxml://` store path becomes one backslash-bearing name → `FileNotFoundException` | note only (companion `:557` `appPath.Replace('\\','/')` is the URL direction and is harmless) |
| B15 | `Rehost.WebForms.Runtime/Compatibility/Resources/ResXResourceWriter.cs:468`, `:470` (`EndsWith("\\")` / `+= "\\"`) | port-owned imported code, but only reached when an application *writes* a `.resx` with a non-empty `BasePath`; the runtime only reads them | the base path handed to `ResXFileRef.MakeFilePathRelative` (which compares against `Path.DirectorySeparatorChar`, `ResXFileRef.cs:137/152`) has the wrong separator, so no prefix ever matches and the file ref is written absolute instead of relative | `Path.EndsInDirectorySeparator` / `Path.DirectorySeparatorChar` (trivial) |
| B16 | `System.Web.Services.ReferenceSource/…/Discovery/DynamicPhysicalDiscoSearcher.cs:85` (`startDir + '\\' + pathRelativ.Replace('/','\\')`) | the file is **not compiled** by any project; and `.vsdisco` has no handler in the shipped config | exclusion paths never match, so excluded directories would be scanned anyway | `Path.DirectorySeparatorChar` — **note the ledger P73 row claims this file was fixed, but the commit `cc83624` only changed line 53 (`ScanDirectory`); `MakeAbsExcludedPath` at :85 still writes `'\\'`.** Either finish the change or amend P73. |

Also in the same uncompiled Services tree, same shape, same disposition
(`DiscoveryClientProtocol.cs:456/460/464`, `WsdlHelpGeneratorElement.cs:163/165`):
physical path/relative-path composition with `"\\"` / `'\\'`. Left with B16 as one
decision because the tree does not compile.

---

## Group C — correct by contract (keep)

Grouped by the reason they are correct.

### C1. Virtual/URL-path normalisation and classification — Framework's own contract

| site | snippet | note |
| --- | --- | --- |
| `Util/UrlPath.cs:30` | `s_slashChars = { '\\', '/' }` | used by `MakeRelative` (`:376`) on virtual paths |
| `Util/UrlPath.cs:33` | `IsRooted`: `basepath[0] == '/' \|\| '\\'` | virtual |
| `Util/UrlPath.cs:87` | `IsAppRelativePath`: `~/` or `~\` | virtual |
| `Util/UrlPath.cs:117` | `IsDirectorySeparatorChar` | feeds `IsAbsolutePhysicalPath`; **P71 boundary, keep untouched** |
| `Util/UrlPath.cs:322` | `virtualPath.Replace('\\','/')` (`FixVirtualPathSlashes`) | virtual |
| `Util/UrlPath.cs:619` | `MakeVirtualPathAppAbsolute`: `~/` or `~\` | virtual |
| `Util/UrlPath.cs:668` | `PathIsDriveRoot`: `l==3 && [1]==':' && [2]=='\\'` | **P71 boundary.** Off Windows always `false`; the Unix root `/` has no equivalent guard in `HostingEnvironment.MapPathActual:1130/1134`, but an application root of `/` is not a supported deployment. Note only. |
| `VirtualPath.cs:587` | `case '\\': slashes = true;` in `Reduce` | virtual |
| `misc/ConfigPathUtility.cs:43` | `if (ch == '\\') return false;` | configuration path (a lowercased virtual path) must not contain `\` |
| `Configuration/WebConfigurationHost.cs:319` | `IsValidSiteArgument` rejects leading/trailing `/` or `\` | config path |
| `Configuration/HttpRuntimeSection.cs:48` | `requestPathInvalidCharacters` default `"<,>,*,%,&,:,\,?"` | URL validation; P72 canonicalises `\`→`/` at the adapter before this runs |
| `Rehost.WebForms.Hosting/RequestPathCanonicalizer.cs:39` | `path.Replace('\\','/')` | **this is P72's http.sys emulation — the whole point** |
| `Rehost.WebForms.Hosting/AspNetCoreWorkerRequest.cs:142` | `virtualPath[0] != '/' \|\| virtualPath.IndexOf('\\') >= 0` | rejects a non-virtual argument |
| `Rehost.WebForms.Runtime/Compatibility/Hosting/ApplicationBootstrap.cs:230` | virtual-root validation rejects `\` | port-owned, correct |
| `Rehost.WebForms.FriendlyUrls/FriendlyUrl.cs:73`, `SwitchViewRouteHandler.cs:66` | `url[1] != '/' && url[1] != '\\'` | app-relative check, mirrors `UrlPath.IsAppRelativePath` |
| `System.Web.Optimization.ReferenceSource/BundleCollection.cs:359` | `bundleVirtualPath.Replace("\\","/")` | virtual |
| `System.Web.Optimization.ReferenceSource/DefaultBundleBuilder.cs:68` | `appRelativeFilePath.Replace('\\','/')` | virtual |
| `System.Web.Optimization.ReferenceSource/FileExtensionReplacementList.cs:71` | `replacementIncludePath.Replace('\\','/')` after `Path.Combine` on a *virtual* path | benign in both directions — see the inverse check below |
| `UI/TemplateParser.cs:2458` | `filename.Replace('/', Path.DirectorySeparatorChar).Replace('\\', …)` | **already fixed, ledger P71** |
| `Util/FileUtil.cs` (`RemoveTrailingDirectoryBackSlash`, `FixUpPhysicalDirectory`, `GetRelativePath`) | `Path.DirectorySeparatorChar` throughout | **already fixed, ledger P56** |
| `Util/UrlPath.cs:658` | `path[l-1] != '\\' && != Path.DirectorySeparatorChar` | **already fixed** (comment in place) |
| `Util/versioninfo.cs:145` | `LastIndexOfAny({ '\\', Path.DirectorySeparatorChar })` | **already fixed** |
| `MimeMapping.cs:49` | `{ Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, Path.VolumeSeparatorChar }` | Framework's own shape; `\` is correctly not a separator off Windows |

### C2. UNC / drive-letter *shape* checks — P71 boundary, keep

`HttpRuntime.cs:327` (`_isOnUNCShare`), `HttpResponse.cs:1147`
(`RemoveOutputCacheItem` rejects `\\…` and `X:`), `UI/WebControls/SiteMapPath.cs:594`
(`node.Url.StartsWith("\\\\")`), `Configuration/FormsAuthenticationConfiguration.cs:398/405`
and `Configuration/PassportAuthentication.cs:138` (reject a UNC or drive-rooted
`loginUrl`/`defaultUrl`/`redirectUrl`), `Configuration/TrustLevel.cs:96/126`
(UNC test). The `X:` drive tests guarding
`Configuration/ExpressServerConfig.cs:372` and `ProcessHostMapPath.cs:279` are
the same shape, but those two lines are counted under B4/B5 for the `"\\"`
they append.
Off Windows these are all constantly `false`, which is exactly right: Unix cannot
spell `X:\` or `\\server\share`, so the string is a virtual path — the documented
P71 outcome.

### C3. String / wire escaping (not paths at all)

`Util/HttpEncoder.cs:88,419-437` (JavaScript string encoding),
`UI/Util.cs:1447-1517` (same), `UI/LOSFormatter.cs` (see D — `NETFRAMEWORK`),
`UI/ClientScriptManager.cs:1221` (`<\/script>`),
`UI/WebControls/MenuRendererStandards.cs:169,179` (CSS `url("…")` quoting),
`Security/AntiXss/SafeList.cs:250` (CSS `\` escape prefix),
`WebSockets/SubprotocolUtil.cs:76` (HTTP token separator),
`httpserverutility.cs:1339` (`AspCompatUrlEncode`: `\` → `%5C`),
`Compilation/AssemblyBuilder.cs:774` (VB `/define:_MYTYPE=\"Web\"` command line),
`Cache/SqlCacheDependency.cs:304,312,345,584` (cache-key `\:` escaping),
`UI/TemplateParser.cs:2510` (`%\>` → `%>`, ASURT 7175),
`Security/ADMembershipProvider.cs:1931,2830,2831,2849,2854,2860,2866,2869,2871`
(LDAP filter escaping), `Configuration/AuthorizationRule.cs:354` (`.\` → machine
name, a Windows account name).

### C4. Regular-expression escapes

`Configuration/BrowserCapabilitiesFactory.cs` (30 sites: 43, 53, 96, 267, 328,
371, 422, 427, 549, 619, 649, 848, 853, 893, 1120, 1147, 1255, 1476, 1617, 1660,
1700, 1705, 1793, 1826, 2086, 2200, 2265, 2270, 2295, 2305),
`Configuration/HttpCapabilitiesSectionHandler.cs:382-394`,
`Configuration/CapabilitiesPattern.cs:34-36`,
`Configuration/HttpCapabilitiesBase.cs:409`,
`UI/WebControls/Adapters/WmlPageAdapter.cs:738-755`,
`UI/WebControls/basecomparevalidator.cs:286,287,288,313,338,474`,
`UI/WebControls/{ChangePassword.cs:88,89, CreateUserWizard.cs:49,50, LoginUtil.cs:18,19, PasswordRecovery.cs:55,56}`,
`UI/CssStyleCollection.cs:30,32,34`, `ErrorFormatter.cs:1259`,
`State/OutOfProcStateClientManager.cs:184`,
`Util/Wildcard.cs:49,50,51,54,106,112,116,246,252,256,273,279,283`
(`:54 backslashRegex` is an unused field),
`System.Web.Optimization.ReferenceSource/{Bundle.cs:374, CssRewriteUrlTransform.cs:44, PatternHelper.cs:24}`.

### C5. Windows account / domain names (`DOMAIN\user`) — not paths

`Security/ADMembershipProvider.cs:673,1078,1108,1274,1303,1321,1783,1818,1847,2610,4113`,
`Security/AuthStoreRoleProvider.cs:704`,
`Security/WindowsTokenRoleProvider.cs:187`,
`DataAccess/DataConnectionHelper.cs:80`.

### C6. Registry key paths — not filesystem paths

`Cache/CacheInternal.cs:309` (also `#if USE_MEMORY_CACHE`, undefined),
`State/SessionStateModule.cs:1094`, `Util/Misc.cs:172,176`,
`Util/Debug.cs:201,202`, `Util/EnableViewStateMacRegistryHelper.cs:67`,
`Util/AspCompat.cs:310`, `Configuration/serverconfig.cs:81,82`.
All are `try`/`catch`-guarded or gated; the registry itself being unavailable off
Windows is a separate, already-accepted boundary.

### C7. Value paths that merely look like file paths

`UI/WebControls/TreeView.cs:68` (`InternalPathSeparator = '\\'` — the control-state
node value path, split/joined in `TreeNode.cs`, `Menu.cs`),
`UI/WebControls/Adapters/MenuAdapter.cs:59,61,68,109` (escaping of that same value
path). Never touches a filesystem.

### C8. Filename validation and named synchronisation

`UI/Util.cs:254` (`invalidFileNameChars = { '/', '\\', '?', '*', ':' }` —
"Windows-strict name rules remain portable application rules",
`docs/filesystem-semantics.md`), `Compilation/CompilationLock.cs:64,69`
(`Global\` / `Local\` mutex prefixes; off Windows the registry lookup returns
`null` under `#if !NETFRAMEWORK`, so only the `Local\` branch is live — the
prefix is honoured on every target, per the repo's cross-process rule).

### C9. Port-owned, deliberately Windows-shaped

`Rehost.WebForms.Runtime/Compatibility/Util/SaveAsPath.cs:28`
(`filename[0] is '\\' or '/'` — detects a *Windows*-rooted path in order to
produce an actionable error off Windows; that is the whole purpose of the file),
`Rehost.WebForms.Runtime/Compatibility/Resources/StronglyTypedResourceBuilder.cs:81`
(`'\\'` in the char set replaced by `_` in generated identifiers),
`Rehost.WebForms.Owin.Host.SystemWeb/SystemWebChunkingCookieManager.cs:181`
(`"…\\G\\M\\T"` — `DateTime` format-string escapes).

### C10. Miscellaneous

`GlobalSuppressions4.cs:5` (prose inside a `SuppressMessage` justification),
`Compilation/ObjectFactoryCodeDomTreeGenerator.cs:62`
(`new CodeLinePragma(@"c:\\dummy.txt", 1)` — a dummy `#line` immediately followed
by `#line hidden`; the file is never opened. The reference source even carries a
`#else` with `@"/dummy.txt"` under `PLATFORM_UNIX`, which the port does not
define. Cosmetic; note only).

---

## Group D — dead

| site(s) | why dead |
| --- | --- |
| `Util/FileEnumerator.cs:77`, `:157` (`_path + @"\"`, `_path + @"\*.*"`) | inside `#if NETFRAMEWORK`; the portable leg uses `DirectoryInfo.EnumerateFileSystemInfos` + `DirectoryOrder` (P73) |
| `UI/TemplateParser.cs:2453` | inside `#if NETFRAMEWORK`; the portable leg at `:2458` is the P71 fix |
| `UI/LOSFormatter.cs:204,514,947,964,965,966,969,973,977` | inside `#if NETFRAMEWORK` |
| `Compilation/MultiTargetingUtil.cs:230,257` | inside `#if NETFRAMEWORK` (registry paths anyway) |
| `Util/versioninfo.cs:68` (`"\\\\?\\"` prefix strip) | in the `#elif !FEATURE_PAL` leg of `#if !NETFRAMEWORK`; the portable leg returns `Environment.ProcessPath` |
| `Util/FileUtil.cs:598` | inside `#if DBG` (undefined) |
| `VirtualPath.cs:52` | inside `#if DBG` |
| `Configuration/BrowserCapabilitiesCodeGenerator.cs:82,83,138,275,788,802,814,818` | `Compile Remove` (8 sites, all physical composition — `_browsersDirectory + "\\" + …`) |
| `Configuration/RemoteWebConfigurationHost.cs:390,401,444` | `Compile Remove` |
| `Management/regiisutil.cs:274` | `Compile Remove` |
| `Configuration/MTConfigUtil.cs:197`, `Configuration/WebConfigurationHost.cs:924` (`@"config\machine.config"` into `ToolLocationHelper.GetPathToDotNetFrameworkFile`) | downlevel-`targetFramework` multi-targeting only; the helper looks up a Windows .NET Framework install and returns `null` off Windows |
| all of `System.Web.Services.ReferenceSource` except `Configuration/{ProtocolElement,ProtocolElementCollection,Protocols}.cs` and `WsiProfiles.cs` | **not compiled by any project** — covers `Discovery/LinkGrep.cs` (17 regex sites), `Discovery/DynamicVirtualDiscoSearcher.cs:119,198`, `Discovery/DynamicPhysicalDiscoSearcher.cs:77,85`, `Discovery/DiscoveryClientProtocol.cs:456,460,464`, `Configuration/WsdlHelpGeneratorElement.cs:163,165`, `Protocols/RequestResponse.cs` (entity table — `\x` escapes, not backslashes) |
| `System.Web.Extensions.ReferenceSource/ClientServices/Providers/ClientData.cs:213` (`_IsolatedDir + "\\" + …`) | not in `Rehost.WebForms.Extensions.csproj`'s file list |
| `System.Web.Extensions.ReferenceSource/Compilation/WCFBuildProvider.cs:487` | not in the file list |

---

## Inverse check: `Path.DirectorySeparatorChar` on virtual paths

Swept `Rehost.WebForms.Runtime/Compatibility/**`, `Rehost.WebForms.Hosting/**`,
`Rehost.WebForms.FriendlyUrls/**`, `Rehost.WebForms.Owin.Host.SystemWeb/**` for
`Path.DirectorySeparatorChar`, `Path.AltDirectorySeparatorChar`, `Path.Combine`,
`Path.GetFileName`, `Path.GetDirectoryName`, `Path.EndsInDirectorySeparator`.

**No defect found.** Every occurrence holds a physical path:
`CanonicalCasePath.cs:31,43,50,65,76,101,111,122,125,132`,
`DirectoryOrder.cs:28`, `DirectoryRequests.cs:55`,
`ApplicationBootstrap.cs:106,111,112,126,131,135,153,155,216,218`,
`CurrentAppDomainHosting.cs:90,92`, `CodegenDirectory.cs:28`,
`GeneratedAssemblyLoader.cs:79,82,87`, `RoslynCSharpCompiler.cs:144,152,227,298,311,320,348`,
`AspNetCoreWorkerRequest.cs:112,159,395`, `ClassicPipelineActivation.cs:84,86`,
`ResXFileRef.cs:137,152,153`, `ResXDataNode.cs:186`.
`AspNetCoreWorkerRequest.cs:159` is the correct direction
(`relative.Replace('/', Path.DirectorySeparatorChar)` on the way from virtual to
physical).

The one *imported* site that mixes the two is
`System.Web.Optimization.ReferenceSource/FileExtensionReplacementList.cs:64-71`,
which calls `Path.Combine` on a **virtual** path and then fixes the separator up
with `Replace('\\','/')`. Its own comment says the default VPP "requires `\`".
On Windows the combine produces `\` and the fixup repairs the client-visible copy;
off Windows the combine produces `/` and both copies are already right. Benign in
both directions, so it stays — but it is the shape to watch if a custom
`VirtualPathProvider` is ever added.

---

## Recorded boundaries

- `SqlConnectionHelper.EnsureDBFile` remains Windows-only with LocalDb/user
  instances; its separator and upper-casing behavior is not a portable path
  contract.
- `UrlPath.PathIsDriveRoot` does not recognize `/`; hosting an application at
  the filesystem root is unsupported.

All other decision rows landed as ledger P74/P75.

---

## Appendix: adjacent compiled trees

`System.Web.Extensions.ReferenceSource` (compiled selectively by
`Rehost.WebForms.Extensions`): the only two physical/virtual composition sites,
`ClientServices/Providers/ClientData.cs:213` and `Compilation/WCFBuildProvider.cs:487`,
are **not in the project's file list**. Everything remaining is regex
(`ui/ScriptRegistrationManager.cs:27,29,30,33`, `ui/ScriptResourceAttribute.cs:33`,
`ui/WebControls/QueryableDataSourceHelper.cs:21,22,24,27`) or JSON/JavaScript
escaping (`Script/Serialization/JavaScriptObjectDeserializer.cs:22,25,297,299,372`,
`JavaScriptSerializer.cs:217,219`, `ui/ScriptManager.cs:1770`). Clean.

`System.Web.ApplicationServices.ReferenceSource`,
`Microsoft.AspNet.Web.Optimization.WebForms.ReferenceSource`,
`Rehost.WebForms.WebServices`, `Rehost.WebForms.ApplicationServices`: zero
backslash literals.
