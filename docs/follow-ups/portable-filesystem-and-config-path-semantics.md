# Portable filesystem and configuration-path semantics

Status: decided 2026-08-06; Story 1 delivered (ledger P56) and Story 2
delivered (ledger P57) the same day. Open remainder listed at the end.

## Problem

Framework's filesystem helpers and path semantics assume Windows: NTFS case
folding, `\` separators, Windows-only name rules, Win32 lookups, and the
`Hidden` attribute. Off Windows those assumptions either break (`\` literals),
silently diverge (case, hidden files), or cannot be answered at all
(physical-vs-virtual path shape, ledger P54).

## Decisions (2026-08-06)

Grilled and ratified; the audit behind them enumerated every live call site of
`FileUtil`, `UrlPath`'s physical-path members, `FindFileData`,
`FileAttributesData`, and `FileEnumerator`.

1. **Case-insensitive everywhere.** Framework's end-to-end case-insensitivity
   is the contract on every platform: `/default.aspx` finds `Default.aspx` on
   Linux too. Resolution canonicalizes at the virtual→physical seam to the
   file's true casing, so downstream consumers (compilation caches, monitors)
   see one canonical spelling and the imported ignore-case comparisons are
   correct rather than aliasing. `Request.Path` keeps the casing the client
   sent, as Framework did. Rationale: consumers rely on it, and the
   pre-decision state (ignore-case bookkeeping over a case-sensitive
   filesystem) was the worst combination.
2. **Collisions: exact first, else error.** Two names differing only by case
   in one directory (impossible on NTFS): an exact-case match is served
   without complaint; a lookup that needs the ignore-case fallback and finds
   more than one candidate fails with an error naming both files and the fix.
   Never a silent pick.
3. **Windows-strict name rules on every platform.** Trailing dot/space,
   embedded `:`, and the long-path checks refuse exactly what Framework
   refused, including on Linux where such names are legal. Same app, same
   answer everywhere; the refused names are an explicit boundary.
4. **Hidden means the Windows flag only.** A file is hidden iff the
   filesystem's `Hidden` attribute says so, on both platforms. Dot-names are
   ordinary files, as they were to Framework — `.well-known` serves and
   enumerates. If Unix tool droppings (`.DS_Store`) ever measurably break
   content enumeration, that gets its own evidence-backed story.
5. **`FileUtil` completes in place.** Direct surgical edits for line-level
   fixes, one conditional region where a whole method body changes, new
   port-owned members for new machinery. No `.Portable.cs` swap file.
6. **Physical-vs-virtual ambiguity: seam on demand.** No shared helper can
   classify a rooted Unix path (P54). Callers that know which side they hold
   get explicit known-physical entry points (the `TransmitFileTranslated`
   pattern); each of the 21 live `IsAbsolutePhysicalPath` call sites migrates
   when a story reaches it, with a test. The audit's caller list is the
   checklist.
7. **The dormant native call goes now.** The two-arg `FindFileData.FindFile`
   still compiles a raw `FindFirstFile`, reachable only from the disabled
   file-change-notification subsystem — a P51-class landmine, fixed in
   Story 1.
8. **Two stories.** Story 1: `FileUtil` completion (mechanical, below).
   Story 2: case-insensitive resolution (new machinery, own design pass).

## Story 1 — FileUtil completion

- `RemoveTrailingDirectoryBackSlash`: `'\\'` literal → separator-aware, with a
  root guard that also holds for Unix roots (16 live callers).
- `FixUpPhysicalDirectory`: `@"\"` append → separator-aware (5 live callers).
- `TruncatePathIfNeeded`: randomized `string.GetHashCode` in a codegen path
  name → stable hash (P38 class).
- Two-arg `FindFileData.FindFile`: portable parent-directory walk (adapted
  from the POC's design, `../Portable.System.Web`) replaces the live
  `FindFirstFile`.

## Story 2 — case-insensitive resolution

Delivered (ledger P57): `CanonicalCasePath.Resolve` at the exit of
`HostingEnvironment.MapPathActual`, the choke point every mapping funnels
through; miss-only activation, exact-first, collision 500, unchanged 404.
Evidence runs on a real case-sensitive filesystem everywhere: the routine
macOS suite mounts a disposable case-sensitive APFS volume
(`CaseSensitiveDirectory`, twinned in the two test projects), Linux uses its
temp directly via `eng/linux-round.sh`, NTFS skips. A warm build cache serves
every casing once one compiled — Framework's own behavior — so the
end-to-end test compiles a page no other request has touched.

## Checklist: physical-vs-virtual call sites (decision 6)

Live `UrlPath.IsAbsolutePhysicalPath` callers, from the 2026-08-06 audit; each
migrates to an explicit known-physical seam when a story reaches it, with a
test. `HttpResponse.GetNormalizedFilename` is done (P54).

- Compilation: `BaseCodeDomTreeGenerator.CreateCodeLinePragmaHelper`,
  `BatchParser.ProcessServerInclude`, `TemplateParser.ProcessServerInclude`.
- Configuration: `PagesSection.CreateControlTypeFilter` (masterPageFile).
- Monitoring (FCN, disabled): `FileChangesMonitor` Start/Stop
  MonitoringFile/Path, `GetFileAttributes` ×2.
- Static files: ~~`HttpResponse.GetNormalizedFilename`~~ (P54).
- Sitemap: `SiteMapNode.CreateVirtualPathFromUrl`,
  `StaticSiteMapProvider.AddNode`, `XmlSiteMapProvider.GetNodeFromXmlNode`.
- UI: `Control.ResolvePhysicalOrVirtualPath`, `Control.OpenFile`,
  `AccessDataSource.GetPhysicalDataFilePath`,
  `MailDefinition.CreateMailMessage` ×2.
- Internal: `UrlPath.CheckValidVirtualPath` (rejects physical shapes; the
  rejection reads the same on every platform for `\`-shaped input).

`IsUncSharePath` besides the above: `HtmlTextWriter.EncodeUrl`.

## Still open (deliberately)

- Enumeration ordering guarantees across filesystems.
- Wildcard assembly loading behavior; never swallow unrelated load failures.
- File-change notification is disabled by design; its native surface
  (`DirMonOpen` etc.) is out of scope until an FCN story exists.

## Evidence

`FileEnumerator`/`FindFileData`/`FileAttributesData` attribute and
enumeration arms are portable since ledger P23. Story 1 and Story 2 carry
their own tests; the POC in the sibling `Portable.System.Web` repo is design
evidence only, and kept the ignore-case and randomized-hash traps this plan
removes.
