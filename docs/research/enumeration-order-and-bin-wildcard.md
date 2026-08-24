# Filesystem enumeration order and wildcard `bin` loading

Evidence for the enumeration and wildcard-loading outcome recorded as ledger
P73 (2026-08-16). The audit and [Framework readings](#framework-readings)
below preserve how the result was derived.

Read-only audit of `src/System.Web.ReferenceSource`,
`src/System.Web.Services.ReferenceSource`, `src/System.Web.Optimization.ReferenceSource`,
`src/Rehost.WebForms.Runtime/Compatibility`, `src/Rehost.WebForms.Hosting`,
`src/Rehost.WebForms.Extensions`, `src/Rehost.WebForms.FriendlyUrls`,
`src/Rehost.WebForms.ApplicationServices`, `src/Rehost.WebForms.WebServices`,
`src/Rehost.WebForms.Owin.Host.SystemWeb`. No build, no test run, no git action.

---

## Part 1 — enumeration order

### 1.1 Two substrates, not one

Nearly all of `System.Web`'s directory reading funnels through **one** port-owned
seam already:

`src/System.Web.ReferenceSource/Util/FileEnumerator.cs:168-183`

```csharp
#else
            if (_entries == null) {
                try {
                    _entries = new DirectoryInfo(_path).EnumerateFileSystemInfos().GetEnumerator();
                }
```

`FileEnumerator` replaced `FindFirstFile`/`FindNextFile` for the port (`#if NETFRAMEWORK`
keeps the Framework body). It is the sole backing of
`MapPathBasedVirtualDirectory.Children` / `.Files` / `.Directories`
(`Hosting/MapPathBasedVirtualPathProvider.cs:207,214,314`), which is what every
`VirtualDirectory` consumer in compilation sees. `EnumerateFileSystemInfos`
returns raw directory order — `FindFirstFile` order on Windows, `readdir` order on
ext4 and APFS.

Everything else is a **direct BCL call** that bypasses `FileEnumerator`:
`DirectoryInfo.GetFiles`, `Directory.GetDirectories`, `Directory.GetFileSystemEntries`,
`Directory.EnumerateFiles`, `Directory.EnumerateFileSystemEntries`. Those are the
sites a `FileEnumerator`-only fix would miss.

### 1.2 Complete call-site inventory

Class column: **OBS** order-observable, **IRR** order-irrelevant,
**SORTED** already sorted by code, **PORT** already handled by the port,
**UNREACHED** the feature is unsupported or the branch is dead here.

| # | file:line | API | Consumer | Order observable as | Class |
|---|---|---|---|---|---|
| 1 | `Util/FileEnumerator.cs:171` | `DirectoryInfo.EnumerateFileSystemInfos()` | the substrate for #2–#12 | see below | **OBS** (root cause) |
| 2 | `Hosting/MapPathBasedVirtualPathProvider.cs:314` (+`:207,214,432,436`) | `FileEnumerator.Create` | `VirtualDirectory.Children/Files/Directories` | propagates to every consumer | **OBS** |
| 3 | `Compilation/CodeDirectoryCompiler.cs:341` | `vdir.Children` | App_Code / App_GlobalResources / App_LocalResources / App_WebReferences build-provider list (`_buildProviders.Add`, `:326,386`) | which duplicate type name is CS0101'd; which resource file is reported duplicate; source order in the App_Code assembly | **OBS** |
| 4 | `Compilation/BuildProvidersCompiler.cs:339` (`WebDirectoryBatchCompiler.AddBuildProviders`) | `_vdir.Files` | batch compilation of a page directory (reached from a normal first request, `BuildManager.cs:1672`) | insertion order into a `Hashtable` (§1.4) → compile order → which page's compile error is cached/reported | **OBS** (partly masked by §1.4) |
| 5 | `Compilation/NonBatchDirectoryCompiler.cs:40` | `_vdir.Files` | `batch="false"` directories; each file compiled separately | which page fails first when several are broken | **OBS** (weak) |
| 6 | `Compilation/ThemeDirectoryCompiler.cs:141` → `:152` `AddSkinFile`, `:157` `AddCssFile` | `vdir.Children` | `PageTheme.LinkedStyleSheets` and `ControlSkins` | **rendered HTML**: `<link>` element order (`UI/PageTheme.cs:113-131`); which `.skin` file is named by `Page_theme_skinID_already_defined` (`Compilation/PageThemeCodeDomTreeGenerator.cs:215-222`) | **OBS** (strongest witness) |
| 7 | `Compilation/BrowserCapabilitiesCompiler.cs:171` | `directory.Children` | App_Browsers `.browser` list | none: the list is re-sorted at `Configuration/BrowserCapabilitiesCodeGenerator.cs:414` before parsing, and dependency order is re-sorted at `Compilation/BuildResult.cs:190` | **SORTED** / **UNREACHED** (App_Browsers is Unsupported — `Compatibility/Compilation/BrowserCapabilitiesCompiler.cs:9`) |
| 8 | `Compilation/WebReferencesBuildProvider.cs:95` | `_vdir.Files` | WSDL/discovery inputs | proxy generation order | **UNREACHED** (Web References build providers unsupported — `Compatibility/UnsupportedWebServicesBuildProviders.cs`) |
| 9 | `Compilation/BuildManager.cs:2822` | `vdir.Directories` | `PrecompileWebDirectoriesRecursive` | precompilation traversal order; first failure reported | **OBS** (precompilation only) |
| 10 | `Compilation/BuildManager.cs:2899` | `sourceVdir.Children` | `CopyStaticFilesRecursive` | none (full copy) | **IRR** |
| 11 | `Compilation/BuildManager.cs:2945`, `:2502` | `FileEnumerator.Create` | copy compiled assemblies to target `bin`; delete precomp target dir | none | **IRR** |
| 12 | `Util/HashCodeCombiner.cs:212`, `:237` | `FileEnumerator.Create` | `AddDirectory` / `AddResourcesDirectory` → the top-level "special files" hash (`BuildManager.cs:631,635,637,639`) and `GetDirectoryHash` (`:55`) | the hash is **order-dependent** → the codegen directory is wiped and everything recompiles whenever directory order changes; two machines never agree | **OBS** (cache-invalidation only) |
| 13 | `Compilation/AssemblyBuilder.cs:1263` | `FileEnumerator.Create` | wipe CBM generated-files dir | none | **IRR** (CBM only) |
| 14 | `Compilation/BuildResultCache.cs:828`, `:905`, `:943` | `FileEnumerator.Create` | `RemoveOldTempFiles`, `RemoveAllCodegenFiles`, `DeleteFilesInDirectory` | none (delete everything matching) | **IRR** |
| 15 | `Compilation/BuildResultCache.cs:473`, `:523` | `DirectoryInfo.GetFiles("*"+baseName+".*")` | mark/remove an assembly and its related files | none | **IRR** |
| 16 | `Compilation/BuildResultCache.cs:774` | `Directory.GetDirectories(_cacheDir)` | `FindSatelliteDirectories` | none (set membership) | **IRR** |
| 17 | `Compilation/BuildManager.cs:2870` | `Directory.GetDirectories(appPhysicalDir)` | `PrecompileThemeDirectories` | which theme compiles first; first theme error wins | **OBS** (precompilation only) |
| 18 | `UI/Util.cs:242` | `Directory.GetFileSystemEntries` | `IsNonEmptyDirectory` — count only | none | **IRR** |
| 19 | `Configuration/CompilationSection.cs:849` | `DirectoryInfo.GetFiles("*.dll")` | wildcard `bin` load — **Part 2** | reference order handed to Roslyn; the pair named by `SR.Ambiguous_type` (`UI/Util.cs:1392`); which bad `bin` file fails startup first | **OBS** |
| 20 | `HttpRuntime.cs:1194`, `:1208` | `DirectoryInfo.GetFiles("*.dll")` / `GetDirectories()` | `PreloadAssembliesFromBinRecursive` — every exception swallowed | none | **IRR**, and gated on `<identity impersonate="true">` (inert here, ledger P07) |
| 21 | `Configuration/BrowserCapabilitiesCodeGenerator.cs:178` | `DirectoryInfo.GetFiles("*.browser")` | machine-level browser tree (`aspnet_regbrowsers`) | none — `_browserFileList.Sort()` at `:414`, then ie/mozilla/opera forced to the head (`:437-450`) | **SORTED** |
| 22 | `Configuration/BrowserCapabilitiesCodeGenerator.cs:532`, `:547`, `:648`, `:650`, `:654` | `GetDirectories()` / `GetFiles("*.browser", …)` | *custom* browser subdirectory trees (`_customBrowserFileLists`) | this list is **not** sorted — subdirectory and per-file parse order decides which `Browser_parentID_Not_Found` fires | **OBS**, but **UNREACHED** (file is `Compile Remove`d — `Rehost.WebForms.Runtime.csproj:19`) |
| 23 | `System.Web.Services/Discovery/DynamicDiscoSearcher.cs:146` | `directory.GetFiles(pattern)` | `<discoveryRef>` element order in a generated dynamic-discovery document | **response body** ordering | **UNREACHED** (`.vsdisco` has no handler in the shipped config; the sibling at `DynamicPhysicalDiscoSearcher.cs:53` also composes paths with a literal `'\\'`) |
| 24 | `System.Web.Services/Discovery/DynamicPhysicalDiscoSearcher.cs:46` | `dir.GetDirectories()` | recursion order for the same document | same | **UNREACHED** |
| 25 | `System.Web.Optimization/BundleDirectoryItem.cs:36-60` | `dirInfo.Files` | bundle content order → **served script/CSS concatenation order** | none — `OrderBy(file => file, VirtualFileComparer.Instance)` at `:60` ("Sort the directory files so we get deterministic order") | **SORTED** |
| 26 | `System.Web.Optimization/BundleDirectoryItem.cs:31` | `dir.Directories` | cache-dependency directory set | none | **IRR** |
| 27 | `System.Web.Optimization/FileVirtualPathProvider.cs:87` | `Directory.GetFiles()` | design-time VPP for bundles | feeds #25, which sorts | **IRR** |
| 28 | `Owin.Host.SystemWeb/Loader/DefaultLoader.cs:500-501` | `Directory.GetFiles("*.dll")` + `("*.exe")` | OWIN `Startup` class discovery across `bin` | which assembly's `Startup` wins when two declare one | **OBS** (low: ambiguity normally throws) |
| 29 | `Compatibility/Util/CanonicalCasePath.cs:63` | `Directory.EnumerateFileSystemEntries` | P57 case fold — collects **all** case-insensitive matches, then throws on >1 | only the order of the two names inside the collision message | **PORT** (ledger P57; deliberate deterministic failure) |
| 30 | `Compatibility/Compilation/RoslynCSharpCompiler.cs:305` | `Directory.EnumerateFiles(dir, "*.dll")` | shared-framework `MetadataReference` list | reference order (matters only for equal-identity conflicts; names are unique here) | **PORT / IRR** |
| 31 | `Compatibility/IisConfig/DefaultDocuments.cs` | *(none)* | default-document probing | candidates come from the merged `<defaultDocument><files>` **config list**, not a directory scan | **PORT** (ledger P67) |
| 32 | `FileChangesMonitor.cs` | *(none reached)* | file-change notification | disabled — every entry point returns at `IsFCNDisabled` (`:1538,1656,1682,1777`) | **UNREACHED** |
| 33 | `AssemblyResourceLoader` (WebResource/ScriptResource) | *(none)* | `WebResource.axd` / `ScriptResource.axd` | resources come from assembly manifests, never a directory | **n/a** |
| 34 | site map, profile, membership, `Web.sitemap` | *(none)* | — | single named files / providers | **n/a** |

No enumeration API appears anywhere in `src/Rehost.WebForms.Extensions`,
`src/Rehost.WebForms.FriendlyUrls`, `src/Rehost.WebForms.ApplicationServices`,
`src/Rehost.WebForms.WebServices`, or `src/Rehost.WebForms.Hosting`.

### 1.3 The order-observable sites, concretely

**Theme CSS is the cleanest witness.** `AddCssFile` order becomes
`LinkedStyleSheets` order becomes `<link>` order in the rendered `<head>`:

`src/System.Web.ReferenceSource/UI/PageTheme.cs:113-131`

```csharp
        internal void SetStyleSheet() {
            if (LinkedStyleSheets != null && LinkedStyleSheets.Length > 0) {
                ...
                foreach(string styleSheetPath in LinkedStyleSheets) {
                    HtmlLink link = new HtmlLink();
```

CSS cascade means a different order is a different page. On NTFS this was
alphabetical; on ext4 it is arbitrary.

**Duplicate skin.** Two `.skin` files declaring the same control type with the
same (or empty) `SkinID` throw, naming *whichever file was parsed second*:

`src/System.Web.ReferenceSource/Compilation/PageThemeCodeDomTreeGenerator.cs:215-222`

```csharp
                if (_controlSkinTypeNameCollection.Contains(skinKey)) {
                    if (String.IsNullOrEmpty(skinID)) {
                        throw new HttpParseException(SR.GetString(SR.Page_theme_default_theme_already_defined,
                            builder.ControlType.FullName), null, builder.VirtualPath, null, builder.Line);
```

**Duplicate App_GlobalResources name.** The resource name is lowercased
(`BaseResourcesBuildProvider.cs:108`), so `Foo.resx` + `Foo.resources` — or, only on a
case-sensitive filesystem, `Foo.resx` + `foo.resx` — collide; the *second* one
processed throws and names itself:

`src/System.Web.ReferenceSource/Compilation/BaseResourcesBuildProvider.cs:119-126`

```csharp
                    outputStream = assemblyBuilder.CreateEmbeddedResource(this, resourceFileName);
            ...
            catch (ArgumentException) {
                // This throws an ArgumentException if the resource file name was already added.
                throw new HttpException(SR.GetString(SR.Duplicate_Resource_File, VirtualPath));
```

**App_Code duplicate type.** `CodeDirectoryCompiler` adds providers in `Children`
order; the C# compiler reports CS0101 against the *later* declaration, so the file
named in the YSOD is the one the filesystem happened to return second.

**The top-level hash.** `HashCodeCombiner.AddDirectory` folds each entry into a
running hash in enumeration order (`Util/HashCodeCombiner.cs:212-217`), and the
result decides whether the codegen directory survives across process starts. Two
runs that see the same `bin`/`App_Code` in different orders compute different
hashes and wipe the cache. Note that `AppSettings.PortableCompilationOutput`
(`:220,251`) — the Framework's own "make compilation output portable" switch —
suppresses timestamps but does nothing about order, so it does not deliver
determinism on a non-sorting filesystem.

### 1.4 A second, independent scrambler: the collections in between

Sorting the filesystem does **not** by itself make batch compilation
deterministic. Two hops downstream re-randomize:

- `WebDirectoryBatchCompiler` keys providers by virtual path in a `Hashtable`
  (`Compilation/BuildProvidersCompiler.cs:269-270`,
  `new Hashtable(StringComparer.OrdinalIgnoreCase)`), inserts in enumeration order
  (`:370`), and then iterates `.Values` (`:483,506`). `StringComparer.OrdinalIgnoreCase.GetHashCode`
  is **randomized per process** on .NET and was not on .NET Framework (this is exactly
  the ledger P38 condition, at a site P38 did not touch). Batch compile order
  therefore differs between two runs of the same application on the same machine.
- `ProcessDependencies` buckets by a `Hashtable` keyed on `BuildProvider` *objects*
  and enumerates it (`:545-551`), i.e. object-identity hash order.
- `CodeDirectoryCompiler` uses `BuildProviderSet` → `ObjectSet` →
  `HybridDictionary` (`Util/ObjectSet.cs:178`), which preserves insertion order
  only while it is a `ListDictionary`; past the cutover (9 entries) it becomes a
  `Hashtable` keyed on object identity. So App_Code preserves directory order for
  small directories and scrambles for large ones — **on Framework too**.

Consequence for the goal: "reproduce Framework's order" is not achievable and was
never stable on Framework beyond small directories. The achievable and useful goal
is *determinism*: same inputs → same order, on every OS and every run.

### 1.5 What Framework's order actually was on NTFS

NTFS stores each directory as a B+ tree (`$INDEX_ROOT`/`$INDEX_ALLOCATION`) keyed
by file name, collated with the volume's `$UpCase` table: each UTF-16 code unit is
mapped through `$UpCase` and the resulting sequences compared ordinally.
`FindFirstFile`/`FindNextFile` walk that index in key order, and
`Directory.GetFiles` on .NET Framework returned that order unmodified. The
observable effect is case-insensitive, uppercase-first-then-ordinal alphabetical
order.

Confidence: **high** for the mechanism and for "NTFS looks alphabetical,
case-insensitively"; this is stable, long-observed behavior. Two caveats stated
explicitly:

- It is an implementation artifact, not a contract. The Win32 and .NET docs both
  say the order is unspecified, and FAT/exFAT (creation order) and ReFS differ.
  Framework applications nevertheless depend on it in practice.
- `$UpCase` is captured at **format** time from the Unicode version of the Windows
  that formatted the volume, so two NTFS volumes can disagree about non-ASCII
  ordering. .NET's `OrdinalIgnoreCase` uses the running runtime's invariant casing
  table instead.

For **ASCII names, `StringComparer.OrdinalIgnoreCase` reproduces NTFS order
exactly** — both uppercase per code unit and then compare ordinally, so digits
precede letters and `_` (0x5F) sorts after `Z` (0x5A) in both. Divergence is
confined to characters whose case mapping changed between the Unicode version
baked into `$UpCase` and the one the runtime carries (Cherokee, Georgian Mtavruli,
various Latin additions). Non-BMP names are unaffected: both compare surrogate code
units without case mapping. A *linguistic* sort (`ArrayList.Sort()`,
`StringComparer.InvariantCultureIgnoreCase`) does **not** reproduce NTFS order —
it ignores punctuation weight and orders `_` before letters.

Note that the two existing sorts in the tree are linguistic:
`Configuration/BrowserCapabilitiesCodeGenerator.cs:414` (`ArrayList.Sort()` →
`Comparer.Default` → current culture) and `Compilation/BuildResult.cs:190`
(`InvariantComparer.Default`). They are deterministic — enough for their purpose —
but they are not NTFS order and they are ICU-dependent.

### 1.6 Proposed seams (narrowest first)

**Seam A — sort inside `FileEnumerator`.** One `#else`-branch change at
`Util/FileEnumerator.cs:171`: materialize `EnumerateFileSystemInfos()`, sort by
`Name` with `StringComparer.OrdinalIgnoreCase` and an `Ordinal` tiebreak (needed
only on case-sensitive filesystems, which can hold `A.cs` and `a.cs` at once —
NTFS cannot), then enumerate. This covers rows 2–14: App_Code, batch page
compilation, themes, App_Browsers, precompilation, and the top-level hash. It is
inside code the port already forked for this exact reason, it is `internal`, and it
is testable without a host. Cost: an array per directory, which
`MapPathBasedVirtualDirectory` already effectively pays.

**Seam B — sort the direct BCL sites that matter.** Rows 17, 19 and 28
(`BuildManager.PrecompileThemeDirectories`, `CompilationSection.LoadAllAssembliesFromAppDomainBinDirectory`,
the OWIN scanner) do not pass through `FileEnumerator`. Give `FileUtil` one
port-owned helper — the file that already owns "portable separator, enumeration,
stable-hash, and parent-walk repairs" per [filesystem semantics](../filesystem-semantics.md) —
and call it from those three. Rows 21–24 are unreached; leave them.

**Seam C — de-randomize the collections (separate decision).** Seams A+B do not
make batch page compilation deterministic (§1.4). Making it deterministic means
changing `WebDirectoryBatchCompiler._buildProviders` from `Hashtable` to an
insertion-ordered dictionary and giving `ProcessDependencies` a stable bucket
order. That is a behavioral change to imported code beyond a platform leaf and is
an architectural decision to surface, not a hotfix.

Recommended comparison everywhere: `OrdinalIgnoreCase`, then `Ordinal`. It matches
NTFS for the ASCII names real applications use, it is culture- and ICU-independent
(unlike the two existing linguistic sorts), and it is consistent with the
`OrdinalIgnoreCase` the port already uses for path identity (P57).

### 1.7 Evidence that would prove a fix — what turns red on Linux today

A single-file check is a coin flip: `readdir` order can accidentally match sorted
order. Use enough entries that accidental agreement is negligible.

1. **Unit, `tests/Rehost.WebForms.Runtime.Tests/Util/FileEnumeratorTests.cs`**
   (beside the existing `FileUtilTests.cs`): create ~10 files in a temp directory in
   reverse-alphabetical creation order, plus `_under.cs` and `9nine.cs` to pin the
   `_`-after-`Z` and digits-first rules, and assert `FileEnumerator.Create` yields
   them `OrdinalIgnoreCase`-sorted. Green on Windows today (NTFS already sorts) and
   on macOS/APFS only by luck; **red on Linux/ext4 today**. Add one case on the
   `CaseSensitiveDirectory` fixture with `A.cs` and `a.cs` to pin the ordinal
   tiebreak. Requires a Linux round (`eng/linux-round.sh`) — a guard for a
   platform difference must run on the platform that triggers it.
2. **Scenario, `App_GlobalResources` duplicate**: add `Strings.resources` beside
   the existing `Strings.resx` in `tests/.../fixtures/codegen/App_GlobalResources/`
   (or a second copy under a distinct case on the case-sensitive volume) and assert
   the `Duplicate_Resource_File` message names a fixed file. Reachable today —
   `App_GlobalResources` is a supported claim — and drives the real
   `CodeDirectoryCompiler` path, not just the enumerator.
3. **Scenario, `App_Code` duplicate type**: two files in `fixtures/codegen/App_Code`
   declaring the same class; assert the compile error names a fixed one. Keep the
   directory at ≤8 files or the `HybridDictionary` cutover (§1.4) reintroduces
   nondeterminism and the test flakes for a different reason — which is itself the
   argument for Seam C.
4. **Theme `<link>` order** is the most user-visible witness but themes are not a
   current compatibility claim; it is the right test to write when they are.
5. **Anti-test**: assert the *top-level hash* (`HashCodeCombiner.GetDirectoryHash`)
   is equal for the same directory contents created in two different orders. This is
   the cache-thrash symptom and is OS-independent once Seam A lands.

### 1.8 Adjacent defects found while auditing (not enumeration order)

- `Compilation/BuildResultCache.cs:825`: `string codegen = _cacheDir + "\\";` — a
  literal backslash, so the `FileUtil.FileExists(codegen + baseName + ".dll")`
  guards below it never match off Windows and `RemoveOldTempFiles` deletes generated
  sources it was meant to keep for debugging.
- `Directory.GetFiles(path, "*.dll")` matches the pattern **case-sensitively on
  Linux** (`MatchCasing.PlatformDefault`; case-insensitive on Windows and macOS). A
  `bin/Foo.DLL` shipped from a Windows build therefore loads on Windows and macOS and
  is silently invisible on Linux. Same for `"*.browser"`. P57 folds *paths*, not
  *glob patterns*.
- `System.Web.Services/Discovery/DynamicPhysicalDiscoSearcher.cs:53` composes
  `localDir + '\\' + subDir.Name`. Unreached, but it is the same class of defect as
  ledger P71.

---

## Part 2 — wildcard `bin` loading (`<add assembly="*"/>`)

### 2.1 The path, end to end

| # | file:line | Role |
|---|---|---|
| 1 | `src/Rehost.WebForms.Runtime/configs/rehost-webforms.web.config:61` | the shipped root config's `<add assembly="*"/>`, last in `<assemblies>` |
| 2 | `Configuration/AssemblyInfo.cs:72-84` | `AssemblyInternal` — lazy, caches the expansion as an `Assembly[]` |
| 3 | `Configuration/CompilationSection.cs:685-699` | `LoadAssembly(AssemblyInfo)` — `if (ai.Assembly == "*") assemblies = LoadAllAssembliesFromAppDomainBinDirectory();` else a single named load. The wildcard branch never calls `RecordAssembly` |
| 4 | `Configuration/CompilationSection.cs:834-880` | the scan (below) |
| 5 | `Configuration/CompilationSection.cs:762-832` | `LoadAssemblyHelper(name, starDirective)` — one `Assembly.Load(simpleName)` per file |
| 6 | `Configuration/AssemblyCollection.cs:82-84` | `IsRemoved(key)` — the only effect `<remove>`/`<clear>` has here |
| 7 | `Compilation/BuildManager.cs:287-320`, `:323-353`, `:367-373` | `GetReferencedAssemblies` — usual forcing point, under `lock (compConfig)` |
| 8 | `Compilation/BuildManager.cs:916-919` | `GetPreStartInitMethodsFromReferencedAssemblies` — the **earliest** forcing point, pre-application-start |
| 9 | `Compilation/BuildManager.cs:1212-1229` | `EnsureTopLevelFilesCompiled` caches the failure in `_topLevelFileCompilationException` and **replays it on every later request** |
| 10 | `UI/Util.cs:1392` | `SR.Ambiguous_type` — where a duplicate type across two `bin` assemblies surfaces, at `BuildManager.GetType`, not at load |
| 11 | `HttpRuntime.cs:1168-1211` | `PreloadAssembliesFromBin` — a second, recursive scan; swallows everything; gated on `<identity impersonate="true">` |
| 12 | `Owin.Host.SystemWeb/Loader/DefaultLoader.cs:481-522` | a third scan, port-rewritten, for the OWIN `Startup` class |

The scan itself (`Configuration/CompilationSection.cs:847-867`):

```csharp
                DirectoryInfo binPathDirectory = new DirectoryInfo(binPath);
                // Get a list of all the DLL's in the bin directory
                binDlls = binPathDirectory.GetFiles("*.dll");
                ...
                        string assemblyName = Util.GetAssemblyNameFromFileName(binDlls[i].Name);
                        if (assemblyName.StartsWith(BuildManager.WebAssemblyNamePrefix, StringComparison.Ordinal))
                            continue;
                        if (!GetAssembliesCollection().IsRemoved(assemblyName)) {
                            assembly = LoadAssemblyHelper(assemblyName, true);
                        }
                        if (assembly != null) {
                            list.Add(assembly);
                        }
```

Four things follow. The scan is **not** `FileUtil` or `FileEnumerator`, so Seam A
misses it. It is `*.dll` only (top level; no `.exe`, unlike the OWIN scanner). The
file name is stripped **textually** (`UI/Util.cs:1364-1370`) with no inspection of
the file — the disk supplies a name, and the load is `Assembly.Load(simpleName)`,
never a load by path. And `assembly` is declared outside the loop (`:838`), so when
`IsRemoved` is true the previous iteration's assembly is appended a second time —
a latent Framework bug that only shows through duplicated references.

### 2.2 What is swallowed and what is surfaced

| file:line | Catch | Disposition |
|---|---|---|
| `CompilationSection.cs:771-787` | `catch (Exception e)` → `Marshal.GetHRForException(e)`; `if (hresult == -2146234344) ignoreException = true;` | **HRESULT equality, not type**: only `COR_E_ASSEMBLYEXPECTED` (0x80131018). Comment: *"This is expected to fail for unmanaged DLLs that happen to be in the bin dir."* |
| `CompilationSection.cs:790-795` | `if (BuildManager.IgnoreBadImageFormatException) { … e as BadImageFormatException … }` | the only **type-based** skip, gated on a precompilation-only flag (`BuildManager.cs:2731-2734`, `PrecompilationFlags` `0x100`). **Inert at runtime.** |
| `CompilationSection.cs:797-826` | everything else | `throw new ConfigurationErrorsException(Message, e, source, lineNumber)`, with `assemblyName` rewritten to `"*"` for line lookup. One bad file in `bin` fails application startup, and #9 above replays it forever |
| `CompilationSection.cs:728` | bare `catch { }` in `LoadAssembly(string, bool)` | swallows everything, then scans `<assemblies>` by simple name; rethrows only when `throwOnFail` |
| `CompilationSection.cs:842-845` | no catch | a missing `bin` is normal — trace only |
| `Util/FileUtil.cs:414-424` (port branch) | `FileNotFoundException`, `DirectoryNotFoundException`, bare | an EACCES on `bin` reports *exists* |
| `HttpRuntime.cs:1196-1204` | `catch (FileNotFoundException) → LoadFrom → catch { }`, then `catch { }` | by design: "Pre-load all the assemblies, ignoring all exceptions" |
| `Owin/DefaultLoader.cs:514-518` | `catch (BadImageFormatException) { continue; }` | only that; `FileLoadException`/`IOException` escape (deliberate — `docs/provenance/aspnet-katana.md:56-59`) |
| — | `ReflectionTypeLoadException` | **not handled anywhere on this path**; the scan never enumerates types |

### 2.3 What the port already changed

- **Nothing in the expansion path.** `CompilationSection.cs`, `AssemblyInfo.cs`,
  `AssemblyCollection.cs`, `AssemblyResolver.cs` carry no `#if NETFRAMEWORK` and no
  post-import commit.
- **Underneath it**: `HttpRuntime.InitFusion` (`HttpRuntime.cs:1030-1061`) replaces
  `AppendPrivatePath`/`SetShadowCopyPath`/`SetCachePath` with
  `GeneratedAssemblyLoader.Install(appPath + "bin", _codegenDir)`
  (ledger P30/P34/P35).
  `Compatibility/Hosting/GeneratedAssemblyLoader.cs` hooks
  `AssemblyLoadContext.Default.Resolving` (`:30`), probes **codegen before bin**
  (`:64-89`), refuses a path with a sibling `.delete` marker (`:93-95`), and loads
  with `LoadFromAssemblyPath` into the default context. So the wildcard's
  `Assembly.Load(simpleName)` reaches a `bin` file only through this fallback
  resolver. Shadow copying is gone; `bin` assemblies stay file-locked.
- **The native-`.dll` problem is already solved once, elsewhere.**
  `Compatibility/Compilation/RoslynCSharpCompiler.cs:332-352`:

  ```csharp
  // The shared framework ships native libraries beside managed assemblies, and only on Windows
  // do they carry the .dll extension. MetadataReference.CreateFromFile defers reading the image,
  // so an unmanaged file is rejected at compilation instead, as CS0009 against every reference.
  private static bool HasManagedMetadata(string path)
  ```

  A `PEReader.HasMetadata` probe. Nothing equivalent guards the `bin` scan.
- No test exists for `<add assembly="*"/>`, `LoadAllAssembliesFromAppDomainBinDirectory`,
  `LoadAssemblyHelper`, `IsRemoved`, or a non-PE file in `bin`. The only related test is
  `tests/Rehost.WebForms.Runtime.Tests/Compatibility/Hosting/GeneratedAssemblyLoaderTests.cs`,
  which covers the port-owned resolver alone.

### 2.4 Remaining cases

| Case | Framework (NTFS/CLR) | Port today | Known? | How to prove |
|---|---|---|---|---|
| Native `bin/foo.dll` (P/Invoke payload, e.g. `SQLite.Interop.dll`) | ignored — CLR raised `COR_E_ASSEMBLYEXPECTED`, matching the HRESULT test | the .NET loader's `BadImageFormatException` for a non-PE/metadata-less file carries `COR_E_BADIMAGEFORMAT` (0x8007000B), not 0x80131018, so the test misses and startup dies with `ConfigurationErrorsException` | **measured both sides** (readings below; .NET 10 `LoadFromAssemblyPath` on an empty, a text, and a native file each: `BadImageFormatException`, HRESULT 0x8007000B) | **unit-testable** on all three OSes: drop a non-PE file named `*.dll` in a `bin`, assert the HResult and the resulting behavior |
| `bin/libfoo.so`, `bin/libfoo.dylib` | n/a | **not scanned** — `*.dll` filter excludes them | known | unit |
| `bin/Foo.DLL` (uppercase extension) | loaded (case-insensitive glob) | loaded on Windows/macOS, **silently skipped on Linux** (§1.8) | known by inspection | **unit**, requires a Linux round |
| x86-only or otherwise architecture-mismatched managed dll | `BadImageFormatException` with `COR_E_BADIMAGEFORMAT` → `ConfigurationErrorsException` (i.e. Framework also fails) | same shape, message text differs | **needs a Framework reading** to pin the exact 4.8.1 message and whether IIS surfaces it as a startup 500 | live host + Windows reading |
| Satellite/resource-only dll at the top of `bin` (`Foo.resources.dll`) | `Assembly.Load("Foo.resources")` — culture-neutral bind of a resource assembly | the port's resolver probes `bin/Foo.resources.dll` only for a **culture-free** name (`GeneratedAssemblyLoader.cs:86-87`), so it does resolve, and a resource-only assembly may then fail to load | **needs a Framework reading**: does 4.8.1 fail startup, or does the HRESULT test absorb it? | **Windows reading** first, then unit |
| Two files, same assembly identity (`A.dll` and `A.copy.dll` both identity `A`) | name/identity mismatch → probing failure | the `Resolving` handler returns an assembly whose identity ≠ the requested name; .NET rejects that with `FileLoadException` | **needs a Framework reading** for the 4.8.1 outcome | unit for the port side; Windows reading for the baseline |
| Same simple name in `bin` **and** in the shared framework / a package reference | the config assembly wins by probing order | the default ALC resolves first and the port's resolver is only a *fallback*, so `bin` **loses** | known (P30 records the intent: runtime-owned wins, reproducing GAC precedence) | unit |
| Duplicate *type* across two `bin` assemblies | `SR.Ambiguous_type` from `UI/Util.cs:1392`, naming both assemblies | identical (imported code), but the pair's order follows the §1.2 row 19 enumeration order | known | live host |
| `bin` subdirectories | not scanned by the wildcard (only `PreloadAssembliesFromBin` recurses) | same | known | unit |
| `bin` unreadable (EACCES) | — | `FileUtil.DirectoryExists` reports *exists*, then `DirectoryInfo.GetFiles` throws `UnauthorizedAccessException`, uncaught | known by inspection | unit (Unix only) |

**Unit-testable** (no host): the enumeration itself, the HRESULT/type of the
exception for each file shape, the `*.DLL` glob, the resolver's candidate policy.
**Live host**: startup failure and its replay across requests, ambiguous-type
resolution, precedence against the shared framework.
**Framework reading needed** — exact cases to run on winbox/IIS Express against 4.8.1:

1. `bin` containing a native `*.dll` — confirm it is ignored and nothing is logged.
2. `bin` containing an x86-only managed dll in a 64-bit pool — capture the exact
   error page and whether the app recovers without a restart.
3. `bin` containing a top-level `Foo.resources.dll` — startup outcome.
4. `bin` containing `A.dll` and a byte-identical `A.copy.dll` — startup outcome and
   whether `A` ends up referenced twice.
5. `bin` containing a zero-byte `Foo.dll` and a text file named `Foo.dll` —
   which HRESULT each produces.
6. Whether `<remove assembly="Foo"/>` before `<add assembly="*"/>` really suppresses
   `bin\Foo.dll`, and whether the duplicate-append bug at `:838` is observable in
   `BuildManager.GetReferencedAssemblies()`.

### 2.5 Proposed seam

Filter before loading rather than widening the catch: in
`LoadAllAssembliesFromAppDomainBinDirectory`, skip any `bin` file whose PE image has
no managed metadata, reusing the `PEReader.HasMetadata` probe that already exists at
`RoslynCSharpCompiler.cs:335` (promoted to a shared internal helper — `FileUtil` is
the natural home). That is one predicate, it is deterministic and cross-platform, it
makes the HRESULT equality test dead rather than wrong, and it leaves every genuine
load failure — missing dependency, version conflict, architecture mismatch —
surfacing as it does today. It directly answers the follow-up's "without swallowing
unrelated load errors".

Pair it with the sort from Seam B so the reference list handed to compilation, and
the pair named by an ambiguity error, are stable.

## Framework readings

IIS 10 + .NET Framework 4.8.9344 on winbox, 2026-08-16. One site, six
applications, each with its own `bin` and a root `web.config` carrying only
`<compilation debug="true" targetFramework="4.8"><assemblies><add assembly="*"/></assemblies></compilation>`
(plus `<remove assembly="B"/>` ahead of it in the last), and a page listing
`BuildManager.GetReferencedAssemblies()`. Managed inputs compiled with
Framework `csc` (`A.dll`; `X86Lib.dll` with `/platform:x86`; `Foo.resources.dll`
from a source carrying `[assembly: AssemblyCulture("de")]`); the native input is
a copy of `winmm.dll`.

| `bin` | Outcome |
|---|---|
| `Native.dll` (native PE) beside `A.dll` | 200; `A` referenced; nothing about `Native` |
| `Zero.dll` (0 bytes) and `Text.dll` (`hello`) beside `A.dll` | 200; `A` referenced; both ignored |
| `Foo.resources.dll` (Culture=de) beside `A.dll` | 200; `Foo.resources, Culture=de` **is referenced** beside `A` |
| `X86Lib.dll` (32-bit-required) beside `A.dll` | 500 Configuration Error: "Could not load file or assembly 'X86Lib' or one of its dependencies. An attempt was made to load a program with an incorrect format." Source line: `<add assembly="*"/>`; replayed on every request |
| `A.dll` + byte-identical `A.copy.dll` | 500 Configuration Error: "Could not load file or assembly 'A.copy' or one of its dependencies. The located assembly's manifest definition does not match the assembly reference. (Exception from HRESULT: 0x80131040)"; same source line |
| `A.dll` + `B.dll`, `<remove assembly="B"/>` before `*` | 200; `A` referenced once, `B` absent — the duplicate-append at `CompilationSection.cs:838` is not visible through `GetReferencedAssemblies()` (an `AssemblySet`) |

`GetReferencedAssemblies()` itself is a set, so reference *order* is not
observable there on either runtime; the reading pins outcomes, not sequence.

## Outcome

Decisions 1–3, 6, 7, 9 taken as recommended; 4 taken as "fix the string-keyed
`Hashtable` now" (insertion-ordered dictionary under `#if !NETFRAMEWORK`, the
object-identity hops left as Framework had them); 5 as "determinism only"; 8 as
"readings first" (above). Landed as ledger P73: `DirectoryOrder`,
`BinDirectoryScan`, `PortableExecutableFile` (the `HasManagedMetadata` probe
promoted from `RoslynCSharpCompiler`), `FileEnumeratorTests`,
`BinDirectoryScanTests`, and the App_Code duplicate-type scenario in
`CodegenCompileErrorTests`.
