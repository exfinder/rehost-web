# IIS unhonored system.webServer sections

## Objective

Close the half-silent gap: a `system.webServer` element the port has no reader
for is ignored today, and the application runs without the behavior IIS gave
it. After this job such an element fails activation naming the file and the
element, and the two compression sections warn once instead, because the port
never compresses whether the element is there or not.

## Mental model

`system.webServer` is honored at the application root only.

- At the root, an element with no reader fails activation. `urlCompression`
  and `httpCompression` are the exception: they warn once (event 12).
- A folder `web.config` honors `handlers`, `validation` and `modules`. Anything
  else fails activation. `modules` stays tolerated because IIS ignores it there
  in silence (MH24).
- Any `system.webServer` inside a `<location>` block fails activation,
  whatever it holds. Nothing reads it today; IIS applies it to the path.
  `system.web` inside `<location>` is untouched: the Framework's own
  configuration system honors it and keeps doing so.

## Decisions

- Allow list is a list of leaf paths of any depth, one place. An element is
  accepted when it is on the way to a leaf or under one; what sits under a
  leaf is that reader's business, as today (collection readers already refuse
  an unknown child).

  ```text
  handlers  modules  validation  defaultDocument  httpErrors  rewrite  staticContent
  httpProtocol/customHeaders
  caching/profiles
  security/requestFiltering/fileExtensions
  security/requestFiltering/hiddenSegments
  security/requestFiltering/requestLimits
  security/requestFiltering/verbs
  warned: urlCompression  httpCompression
  ```

  `security/authorization` is not on the list, and `NativeAuthorization`
  keeps refusing it first with its own message, which names the substitute
  and also covers `<handlers accessPolicy>`.
- The four per-section root-only checks go away: `RefuseInsideLocation` and
  `RefuseBelowTheRoot` calls for `customHeaders`, `httpErrors`, `clientCache`,
  and `RewriteSection`'s `//location//rewrite` refusal, with the two helpers
  and `RootOnlyRule` themselves once nothing calls them. Their tests move to
  the new theory. `NativeAuthorization.Refuse` stays as it is.
- The walk runs on the application files only, before the readers. The
  shipped baseline is not walked.
- No frozen sample `Web.config` changes. `WingtipToys.Host/Web.Rehost.config`
  removes the `elmah.axd` location (Elmah has no portable build, the transform
  already strips its modules). `AjaxControlToolkitSampleSite.Host/Web.Rehost.config`
  removes the `Temp` location (the port ignored it before; the block cleared
  handlers and modules for a folder the frozen tree does not serve).
- Backlog, sized, not built: `handlers` inside a `<location>` block feeding
  `IisFolderHandlers` (about two days, needs winbox readings for file-path
  locations, location-versus-folder-file order, and `modules` in a location);
  `<location path=".">` and `path=""` folded into the root before reading
  (the CMS and old publish-wizard wrapper, none of the sample apps carry it).
- No reading or ledger IDs in code, config comments or test names. No
  `Co-Authored-By` footers. Comments only for a hidden constraint.

## Messages

`configPath` is the full path of the file read, as every refusal prints it.

Root, unhonored element (the element is its path under `system.webServer`):

```text
<security/authentication> in '<configPath>' is not supported by this port. Remove it. Where a counterpart exists, it belongs in the host's ASP.NET Core pipeline in Program.cs, before UseRehostWebForms().
```

Any `system.webServer` inside `<location>`:

```text
<system.webServer> inside <location path="Temp"> in '<configPath>' is not supported by this port; system.webServer is honored in the application root only. Move the block to the root, or to a web.config in that folder.
```

Folder file, unhonored element:

```text
<staticContent> in '<configPath>' is not supported by this port; a folder web.config honors <handlers>, <validation> and <modules> only. Move it to the application root.
```

Warning, event 12, generic `(file, element, reason)`, message
`"<{1}> in {0} is not honored; {2}"`; the compression reason:

```text
this host does not compress responses. Add Response Compression middleware in Program.cs, before UseRehostWebForms().
```

All refusals are `ConfigurationErrorsException`, like the readers.

## Architecture

Runtime only. Nothing under `System.Web.ReferenceSource` changes.

`Compatibility/IisConfig/UnhonoredSections.cs`, new, static:

```text
static IReadOnlyList<string> RefuseAtRoot(XmlDocument document, string configPath)
    // walks /configuration/system.webServer/* against the leaf list,
    // refuses //location/system.webServer, returns the warned element names found
static void RefuseInFolder(XmlDocument document, string configPath)
    // walks /configuration/system.webServer/* against handlers, validation, modules;
    // refuses //location/system.webServer the same way
```

One recursive walk over element children: an element whose path equals a leaf
or is a prefix of one is accepted (recursing only in the prefix case); a path
under a leaf is never visited; anything else throws. The two entry points
differ by list only.

`IisServerConfiguration.ApplyFile`, application branch: `RefuseAtRoot` runs
first, before `ClassicSectionValidation`; the returned names land in
`Sections` and reach the record as `WarnedSections`. `ReportUnsupported`
emits event 12 for each, beside the caching-profile report, so the warning
comes out where activation reports today, through the same `Swallow` path.

`IisFolderHandlers.Apply`: `RefuseInFolder` replaces the four
`RefuseBelowTheRoot` calls and `RewriteSection.RefuseBelowTheRoot`;
`NativeAuthorization.Refuse` stays.

`WebFormsRuntimeEventSource` gains event 12 `SectionNotHonored(file, element,
reason)` and `WebFormsRuntimeLogger` its `LoggerMessage` twin, the event 11
shape.

## Tests

Unit only, `Runtime.Tests/Compatibility/IisConfig/UnhonoredSectionsTests`,
through `IisServerConfiguration.Load` over a `TemporaryApplication`, the
pattern the deleted tests used.

- One theory over the refusals, each row asserting the file and the element
  text: `<directoryBrowse>` at the root; `<security><authentication>` under a
  supported parent; `<httpProtocol><redirectHeaders>` beside a supported
  sibling; `<location path="Temp"><system.webServer><handlers>`; a folder
  `web.config` with `<staticContent>`. The four deleted tests' cases
  (`customHeaders`, `httpErrors`, `clientCache`, `rewrite` inside `<location>`
  and in a folder file) fold in as rows.
- One fact: a `web.config` carrying every leaf on the list, `verbs` and
  `requestLimits` included, loads clean and no event 12 is raised.
- One fact: `urlCompression` and `httpCompression` load clean, and
  `ReportUnsupported` raises event 12 once per element naming it, captured
  with `RuntimeEventCollector` the way `CachingProfilesReportTests` does.

The `webserver` fixture keeps its `<urlCompression>` line: it was put there to
prove the host activates with an unhonored section, and that is now the
warn path. Nothing asserts on the warning there.

## Docs

- `docs/compatibility.md`: row "Unhonored `system.webServer` sections" flips
  from Unassessed to Refused with the mental model above and the seven names
  IIS lets an application set that the port refuses or warns on
  (`directoryBrowse`, `httpRedirect`, `applicationInitialization`,
  `httpProtocol/redirectHeaders`, `tracing/traceFailedRequests` refused;
  `urlCompression`, `httpCompression` warned); one sentence that the 23
  sections locked in IIS's own `applicationHost.config` cannot be set by an
  application there either. Row "Per-folder `system.webServer`, other
  sections" says the rest now fails activation. A new row on static
  compression: stock IIS gzips static files by default
  (`StaticCompressionModule` is installed, dynamic is not), the port never
  compresses, Response Compression middleware in `Program.cs` is the
  counterpart; unmeasured on the wire, the golden config is the evidence.
- `docs/portability-ledger.md`: P106 for the rule; P100, P101, P104, P105 test
  columns point at `UnhonoredSectionsTests` where they named the deleted ones.
- `docs/backlog.md`: the two sized items above.
- `docs/research/README.md` or the plans index: nothing new to index; no
  readings were taken.

## Sequence

1. Runtime: the walker, event 12 and its logger twin, the `ApplyFile` and
   `IisFolderHandlers` wiring, the four deletions. Unit tests green.
2. The two XDT transforms. Both apps' smoke as in the apps recipe on macOS.
3. Docs.
4. Windows and Linux rounds on the committed head; ff-merge to main gated on
   round exit codes.

## Done when

A `web.config` with `<directoryBrowse>` at the root, `<authentication>` under
`<security>`, any `system.webServer` inside `<location>`, or `<staticContent>`
in a folder file stops the process at start-up naming the file and element;
`<urlCompression>` starts and logs event 12 once; the four old refusal tests
are gone and their cases live in the new theory; both edited sample apps
smoke; three rounds green.
