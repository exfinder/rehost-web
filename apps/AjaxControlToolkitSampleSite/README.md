# AjaxControlToolkitSampleSite

The AJAX Control Toolkit sample site (archived v20.1), running on the ported
runtime from packages. Exploratory spike, not a milestone: the port's first
**Web Site** consumer and its first large third-party control library.

Nothing here is a support claim; [`docs/compatibility.md`](../../docs/compatibility.md)
remains the only one. The pre-import analysis is
[`docs/research/ajaxcontroltoolkit-samplesite-portability.md`](../../docs/research/ajaxcontroltoolkit-samplesite-portability.md);
this file records what actually happened when it ran.

## Layout

| Folder | Role |
| --- | --- |
| `AjaxControlToolkitSampleSite/` | The frozen Web Site project, copied verbatim from the read-only clone. Never modified. |
| `AjaxControlToolkit/` | The toolkit recompiled under Rehost identity, designers intact. |
| `AjaxControlToolkit.HtmlEditor.Sanitizer/` | The sanitizer recompiled against modern HtmlAgilityPack. |
| `AjaxControlToolkitSampleSite.Host/` | The process: a ~20-line Kestrel host, plus the XDT and the Web Site staging step. |

There is no `.App` project. This is a **Web Site**, not a WAP: pages carry
`CodeFile=`, code lives in `App_Code/`, and `Global.asax` is inline. All of it
compiles at runtime, so the sources ship as site content rather than as a
prebuilt app assembly.

## Commands

```text
dotnet build apps/AjaxControlToolkitSampleSite/AjaxControlToolkitSampleSite.slnx
dotnet run --project apps/AjaxControlToolkitSampleSite/AjaxControlToolkitSampleSite.Host
# http://127.0.0.1:5084/ (pass a URL as the first argument to change)

apps/AjaxControlToolkitSampleSite/smoke.sh                       # journey against the running host
eng/app-linux-smoke.sh AjaxControlToolkitSampleSite 5084         # the same, in a Linux container
```

`smoke.sh` walks the landing page and sitemap, four extender demos, the `.asmx`
`[ScriptService]` the AutoComplete extender calls, an UpdatePanel async
postback, and one `ScriptResource.axd` script and one `WebResource.axd`
stylesheet asserted on content. It sends a browser user agent deliberately —
see partial rendering below.

## Result

**48 of the 50 sitemap pages render, on macOS arm64 and Linux, identically.**
The two failures share one cause, and it is the application's own (below).

Verified working, in rough order of how unproven each was going in:

- **`ExtenderControl` registration and descriptor emission.** Every demo
  depends on it and nothing had exercised it. Pages emit
  `Sys.Extended.UI.<Control>Behavior` descriptor blocks.
- **`ScriptResource.axd` for a third-party assembly at scale.** The Accordion
  page alone pulls nine embedded toolkit scripts (2 KB to 128 KB) out of
  `AjaxControlToolkit.dll`, alongside `System.Web`'s own MicrosoftAjax.
- **`WebResource.axd` stylesheets** from the same assembly, with the
  toolkit's `[ClientCssResource]` links.
- **The design-time attribute closure.** `TypeDescriptor.GetProperties` runs
  over every extender on every render, materialising the 51 `[ToolboxBitmap]`
  and 10 `UITypeEditor` attribute sites. Inert on macOS and Linux, on the
  markers published in `605c035`. The 50 designer files never had to be severed.
- **Runtime compilation of the Web Site model** — `App_Code`, `CodeFile`,
  inline `Global.asax`, and the assembly-less `<pages><controls>` entry that
  resolves `InfoBlock` against the `App_Code` assembly.
- **`.asmx` `[ScriptService]` JSON**, `XmlSiteMapProvider` + `SiteMapDataSource`,
  and the `system.webServer/handlers` entry for `AjaxFileUploadHandler.axd`
  (reached; its own context-key guard rejects a bare GET).
- **102 `App_Data/ControlReference/*.html` reads per page render**, built from a
  markup attribute. The research doc left case sensitivity unverified; the Linux
  sweep clears it.
- **`ColorTranslator.ToHtml` off Windows.** The research doc flagged this as
  unverified and Windows-gated. The demo sets `UploadingBackColor="#CCFFFF"`, so
  `AsyncFileUpload.DescribeComponent` calls it on render, and the descriptor
  carries `"uploadingBackColor":"#CCFFFF"` on both platforms.

## web.config

The staged `web.config` comes from `AjaxControlToolkitSampleSite.Host/Web.Rehost.config`,
which replaces the package default wholesale and so repeats its `<runtime>` and
`<system.codedom>` removals first. Five app-specific edits, every one of them
forced by a measured failure:

| Edit | Why |
| --- | --- |
| Remove `<trust level="Medium"/>` | `ApplicationConfigurationPreflight` refuses any level but `Full`; activation never completes. |
| Insert `<httpRuntime targetFramework="4.5"/>` | Absent upstream. Without it every request 500s naming the setting (ledger P40). |
| `useStaticResources="false"` | `true` points every toolkit script, style and image at `~/Scripts/AjaxControlToolkit` and `~/Content/AjaxControlToolkit`, which are output of the Windows-only `LinkStaticResources` hard-link tool and do not exist in the frozen tree. `false` is the toolkit's own default and serves the same assets embedded. |
| `renderStyleLinks="true"` | With `false`, the only carrier of toolkit CSS is the `~/Content/AjaxControlToolkit/Styles/Bundle` that `Layout.master` renders, and that bundle 404s (below). `true` makes each control emit its own `WebResource.axd` link. |
| `ValidationSettings:UnobtrusiveValidationMode = None` | Consequence of the `<httpRuntime>` insertion; see below. |

### `targetFramework` is 4.5, not 4.8.1, and that is load-bearing

The site declares `<compilation targetFramework="4.0"/>` and **no `<httpRuntime>`
at all**, so on Framework it runs with 4.0 quirks. The port requires 4.5 or
later, so inserting the element necessarily moves the application off the
semantics it was authored against. Two 4.5+ behaviors then break it, and the
choice of value decides one of them.

**At 4.7.2 and above, every demo page fails to compile.**
`ParseRecorder.CreateRecorders` installs `WebObjectActivatorParseRecorder` when
`TargetsAtLeastFramework472`, and that recorder emits its cast and `typeof`
through a bare `new CodeTypeReference(ctrlType)` — no `global::`, unlike the
field declaration and the `new` in the same generated method. `App_Code`
declares `namespace InfoBlock { public class InfoBlock }` and every page hosts a
`<samples:InfoBlock>`, so the generated `(InfoBlock.InfoBlock)` binds `InfoBlock`
to the class and fails `CS0426`. This is unmodified Reference Source, so
Framework 4.8.1 would do the same; the frozen site never meets it because it
targets 4.0. `4.5` keeps the recorder out and is the closest available value to
the application's intent.

**At 4.5, `UnobtrusiveValidationMode` defaults to `WebForms`,** which demands a
`jquery` `ScriptResourceMapping` the site never registers; `MaskedEdit` and
`ValidatorCallout` threw in `BaseValidator.OnPreRender`. The `appSettings` entry
restores the 4.0 default. Both effects are the same shape: a 4.0 application
being run under a runtime with a 4.5 floor.

## Consumer contract used here

- `RehostSiteContentRoot` on the Host turns on staging, the XDT pipeline and the
  `dotnet run` redirection, exactly as for a WAP.
- Host dependencies reach the staged site's `bin/`. The Host builds with
  `OutDir=rehost_root/bin/`, which is the staged site's own `bin/`, so
  `ProjectReference` outputs are included: `AjaxControlToolkit.dll`,
  `AjaxControlToolkit.HtmlEditor.Sanitizer.dll` and `HtmlAgilityPack.dll` all
  land there with no extra work. The sanitizer is also in the Host's
  `deps.json`, so the `Type.GetType("…, AjaxControlToolkit.HtmlEditor.Sanitizer")`
  in `EditPanel` can resolve it by simple name.
- `packages.config` maps as usual: `Microsoft.AspNet.Web.Optimization` →
  `Rehost.WebForms.Optimization` on the Host (`Antlr`, `WebGrease` and
  `Newtonsoft.Json` come with it), `HtmlAgilityPack` → nuget.org, and
  `Microsoft.Web.Infrastructure` is dropped as in every other app here.
- **`AjaxControlToolkit.StaticResources` is deliberately not ported.** Its only
  content is a `PreApplicationStartMethod` that registers script mappings to the
  physical paths `useStaticResources="false"` exists to avoid, plus two bundles
  over the same missing files. Including it would register bundles whose
  includes match nothing.

## Known broken, and deferred

**`HoverMenu` and `ReorderList` 500 on any non-Windows filesystem — fixed in
our copy, diverging from upstream** — `App_Code/TodoXmlDataObject.cs:62,148`
read `Path.Combine(RootPath, @"App_Data\TodoItems.xsd")`. The backslash is a
literal in the application source, so `Path.Combine` yielded a single filename
containing a `\`. An application defect, not a port gap; upstream works on
Windows only, and no XDT reaches `App_Code`. Both lines now pass `"App_Data"`
as a separate `Path.Combine` segment — the tree's only deviation from upstream.
(The research doc's backslash scan missed it — it did not cover `App_Code`.)

**The Web Site model needs a staging step the package does not provide.** The
hosting targets exclude `**/*.cs` because they were written for a WAP, where the
sources are already compiled into the app assembly. A Web Site ships them as
content, so without them the staged site has no code behind any page. The Host
carries a local `StageWebSiteSources` target as the smallest unblocking
workaround. The real fix belongs to the open
[project models](../../docs/follow-ups/web-site-vs-wap-project-models.md)
follow-up, which already names Web Site publish as unscoped — this spike is the
first evidence of exactly what is missing.

**Production Optimization remains unvalidated.** `Global.asax` sets
`BundleTable.EnableOptimizations = true`, but its only bundle is registered over
`~/Scripts/WebForms/MsAjax/*.js`, files the site does not contain and no page
references. `GET /bundles/MsAjaxJs` returns **200 with a zero-byte body** rather
than failing. The production path is therefore reached and does not throw, but
nothing measurable passes through WebGrease. The backlog item stands.

**Partial rendering is browser-capability gated.** `ScriptManager` resolves
`SupportsPartialRendering=false` for curl's default agent, and an async postback
then returns the Framework error text rather than a delta. Correct behavior, but
it means any HTTP-level test of an UpdatePanel must send a browser user agent;
`smoke.sh` does.

## Console errors

Every page logs exactly two, and they are the same two everywhere:

```text
Failed to load resource: the server responded with a status of 404 (Not Found)
```

```text
GET /Content/AjaxControlToolkit/Styles/Bundle   404
GET /Scripts/AjaxControlToolkit/Bundle          404
```

Nothing else fails. Fetching every `script`/`link`/`img` URL referenced by all
50 pages resolves 234 distinct assets, and these two are the only non-200s;
there are no JavaScript exceptions on any page.

`Layout.master:10` renders the style bundle and `:17` carries a `ScriptReference`
to the script bundle. Both names are registered by
`AjaxControlToolkit.StaticResources`, which this port omits, so nothing claims
either path and the static handler 404s.

**They are inert.** `Sys.Extended` and the behavior types are defined,
`Sys.Application` reports its components, and on `/ModalPopup` calling `show()`
on the live `Sys.Extended.UI.ModalPopupBehavior` renders the popup and its modal
background. Scripts arrive over `ScriptResource.axd` and styles over
`renderStyleLinks`, so the two tags carry nothing the page needs.

**No fix is available in this shape,** and porting `StaticResources` would not
be one. `ToolkitResourceManager.GetScriptPaths` returns
`~/Scripts/AjaxControlToolkit/Release/*.js` unconditionally — it never consults
`useStaticResources` — so the bundle would register over ~90 files that the
`LinkStaticResources` hard-link tool never produced here. On the evidence of
`/bundles/MsAjaxJs`, which is registered the same way over missing files, that
converts each 404 into a 200 with a zero-byte body: quieter console, same
absence of content. The markup itself is in the frozen tree, where no XDT
reaches. The honest closure is the `LinkStaticResources` replacement the
research doc already lists as gap 5.

## Not exercised by this site

The sanitizer compiles and ships, but only
`HtmlEditor/EditPanel` consumes `IHtmlSanitizer` and the site has no
`HtmlEditor` page — only `HtmlEditorExtender`, which does not sanitize. The
`AjaxFileUpload` and `AsyncFileUpload` upload journeys, client callbacks from
`Rating`, and `Twitter` (a dead outbound API) are unmeasured here; every page
above was walked as a GET, so no demo's postback behavior beyond
`ConfirmButton`'s is covered.
