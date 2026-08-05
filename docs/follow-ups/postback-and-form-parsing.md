# Postback and form parsing

Status: implemented and verified on macOS `arm64` and Windows `x64`. The
oracle-session commitment is resolved below (2026-08-05) under ADR 0044/0045. Scope: urlencoded form parsing,
view state, control state, a postback that changes rendered output, multipart,
saving uploaded content to disk, and the request collection key limit. Sits on the
[entity-body bridge](request-entity-body-bridge.md).

## What the managed path already provided

`FillInFormCollection`, `FillInFilesCollection`, `HttpValueCollection`,
`MultipartContentParser`, `HttpPostedFile`, and `HttpFileCollection` needed no
change. Neither did `Page`'s postback sequencing. No imported source was edited
for this slice; the only production change is the auto-generated key diagnostic
(ledger P15), which lives in port-owned preflight.

## Recovered Framework contract

- View state is **always encrypted**, not merely signed, once
  `AspNetCryptoServiceProvider` is the provider — which P40 guarantees.
  `ObjectStateFormatter` calls `Protect` when encryption *or* MAC is requested,
  and `NetFXCryptoService.Protect` has no sign-only path: it emits
  `IV || Enc(clearData) || Sign(...)` with a fresh `GenerateIV()` per response.
  `predictableIV` is set only for `CryptoServiceOptions.CacheableOutput`, which
  the page path never requests.
  [`ObjectStateFormatter.cs`](../../src/System.Web.ReferenceSource/UI/ObjectStateFormatter.cs#L378),
  [`NetFXCryptoService.cs`](../../src/System.Web.ReferenceSource/Security/Cryptography/NetFXCryptoService.cs#L65)

  So `__VIEWSTATE` and `__EVENTVALIDATION` bytes are not reproducible across two
  runs of the *same* runtime. A rendered page carrying a server form can never be
  compared byte-for-byte against the Framework oracle, for reasons unrelated to
  key material or to P38.
- The view state key derives from purpose **strings** — `"TemplateSourceDirectory:
  ..."` and `"Type: ..."` upper-cased — not from `GetClientStateIdentifier`. The
  P38 hash divergence therefore touches only the separate
  `__VIEWSTATEGENERATOR` field (ledger P48), never the payload.
  [`ObjectStateFormatter.cs`](../../src/System.Web.ReferenceSource/UI/ObjectStateFormatter.cs#L185)
- `GetTypeHashCode`, which *is* serialized inside view state, rides on
  `HashCodeCombiner.AddObject(string)` → `StringUtil.GetStringHashCode`, a stable
  custom algorithm identical on both runtimes.
- Changed events run **after** `Load`: `Init` → `LoadControlState` →
  `LoadViewState` → `ProcessPostData` → `Load` → `ProcessPostData` again →
  `RaiseChangedEvents` → `RaisePostBackEvent`.
- A suppressed MAC failure makes `IsPostBack` return **false**, so a cross-page
  payload renders the page as if freshly requested.
  [`Page.cs`](../../src/System.Web.ReferenceSource/UI/Page.cs#L1671)
- Event validation is bound to the exact `__VIEWSTATE` string it was issued with,
  so a payload cannot carry one page's view state and another's event validation.
  This is why the cross-page probe posts a bare payload that reaches no
  `ValidateEvent` call.
  [`ClientScriptManager.cs`](../../src/System.Web.ReferenceSource/UI/ClientScriptManager.cs#L1295)
- In `requestValidationMode` 4.0 or later, `<pages validateRequest="false" />`
  alone does not disable request validation; `requestValidationMode="2.0"` is
  also required.

## Fixture contract

The fixture declares a bare literal `<machineKey>` — no `,IsolateApps`, which
would mangle the first four bytes with a comparer hash that has no portable
equivalent — and `aspnet:AllowInsecureDeserialization="false"`, which forces MAC
enforcement and the generator field on rather than inheriting them from the host
machine's registry state. See
[machine key and ViewState bootstrap](machine-key-and-viewstate-bootstrap.md).

`Counter` carries control state and view state at once and is declared with
`EnableViewState="false"`, so one postback distinguishes the two mechanisms. The
control-state claim needs **two** rounds: after a single click the counter reads
1 whether or not anything was restored.

## Required evidence

Kestrel scenario coverage renders the page, scrapes the form it rendered, and
posts that back over a real socket — no recorded body — covering form values,
view state, control state under disabled view state, output changing, both the
control-name and `__EVENTTARGET` postback routes, event ordering, multipart form
fields and file reads including empty and unnamed parts, a tampered view state
failing MAC validation, a cross-page view state suppressed rather than thrown,
and request validation rejecting markup.

Every test was confirmed to fail with the behavior removed, by failing test name.

## Oracle-session resolution (2026-08-05)

The pre-demotion commitment to a full golden oracle session is retired, judged
under ADR 0044's evidence rule and ADR 0045's capture-over-golden policy:

- Markup comparison "excluding the three crypto fields" would require a
  redaction layer — exactly the normalization the parity comparison forbids.
  The golden-session instrument is architecturally wrong for pages carrying a
  server form; captures and pinned rendered output are the right one, and both
  exist (`Framework.postback` is a standing gate; redacted-markup byte
  identity is a recorded measurement in
  [mixed farm and incremental migration](mixed-farm-incremental-migration.md)).
- Postback event sequencing is decided by verbatim imported source — no
  imported file changed in this slice — so port-local tests gate it, per the
  ADR 0043 reasoning ADR 0044 generalizes.
- The reverse payload direction (port-rendered `__VIEWSTATE` accepted by a
  Framework node) is the one genuinely Framework-execution-only claim; its
  disposition is owned by the mixed-farm follow-up.

Ledger P43's two readings were taken on .NET Framework 4.8.1 (release 533509,
`win-oracle`, 2026-08-05): `AspNetEnforceViewStateMac` is present and set
to 1, and a page with a server form renders `__VIEWSTATEGENERATOR` there
(the `CA0B0334` measurement). The port's fail-safe — enforcement always on,
generator field always rendered — matches the observed machine state. The
key-absent case stays recovered-from-source; the fixture keeps pinning
`aspnet:AllowInsecureDeserialization` so no test inherits patch state from a
host.

## Saving to disk (2026-08-05)

`HttpPostedFile.SaveAs` writes one uploaded part; `HttpRequest.SaveAs` writes
the whole raw request, optionally behind its request line and headers. Both were
imported unchanged and neither needed porting; what they needed was coverage and
one platform decision.

- The rooted-path guard is `Path.IsPathRooted`, and its answer is the running
  platform's. `requireRootedSaveAsPath` defaults to true, so a relative path is
  refused everywhere with Framework's own message.
- A path written for Windows — drive letter, leading backslash, or UNC share —
  is rooted on Windows and rooted nowhere else. Off Windows it can name no file,
  so Framework's check would refuse it as merely "not rooted", and an
  application that had turned the guard off would silently create one file whose
  name is the entire path. Framework never ran off Windows, so there is no
  behavior to preserve there; ledger P50 adds one port-owned check ahead of
  Microsoft's, naming the real problem and the fix. Windows is untouched.
- Saving after reading is empty by construction, on both runtimes: reading a
  posted file through a `StreamReader` closes the `HttpInputStream`, `Uninit`
  drops its content reference, and the later `WriteTo` writes nothing into a
  file it still creates. The fixture page saves in its click handler, before
  anything renders, which is also the shape a real application has.
- Content above `requestLengthDiskThreshold` lives in a temp file rather than a
  byte array, and saving streams out of it. That branch is covered on the body
  fixture, whose threshold is one kilobyte, and the probe reports whether the
  content really was file-backed so the test cannot pass from memory.

Evidence: `SaveAsPathTests` for the rule; `UploadSaveOverKestrelTests` for a
page saving an in-memory upload, an empty upload, a refused relative path, and
the platform decision; `RawRequestSaveOverKestrelTests` for a spilled upload and
for the raw request with and without its header block. Each test reads the file
back and compares bytes. No Framework reading was taken: the default, the
message, the overwrite behavior, and the saved layout are all in the imported
source, and the header block a Framework host would write is not a baseline this
port can be compared against directly. Whether both hosts sort every header into
the same known/unknown group is unverified, and reaches only
`HttpRequest.SaveAs`.

## Request collection key limit (2026-08-05)

The cap MSRC 12038 added against the 2011 hash-collision denial of service is
opt-in, not on: .NET Framework 4.8.1 reads `Int32.MaxValue` and ships the key in
no configuration file on the machine — every `*.config` under the framework's
config directory was searched (measured on `win-oracle`,
`System.Web` 4.8.9319.0, by reflection over `AppSettings.MaxHttpCollectionKeys`).
This port carries the same constant and ships no key, so it matches without
having decided anything. Nor does the Visual Studio Web Forms template emit it:
the generated 4.8.1 sample application beside this checkout declares no
`appSettings` at all, so a newly created application is uncapped too.

Staying uncapped is deliberate. The attack the counter was a stopgap for depends
on predictable string hashing, and .NET randomizes it per process with no opt-out
— the same randomization ledger P38 had to work around. Capping by default would
also reject requests Framework accepts. The asymmetry worth knowing: a native
ASP.NET Core application is capped at 1024 form values by `FormOptions`, and that
limit never applies here, because System.Web parses the body itself and the host's
form parser is never called.

No standing test guards this: the check and its four call sites — query
string, urlencoded form, multipart form fields, posted files — are untouched
imported source over exercised substrate, so the measurement above and the
compatibility-map row are the record (writing-tests rung 0; a dedicated
fixture and five tests were built first and removed by that rule). Three traps
for whoever tests near it later, learned from red runs: the check runs before
each add, so the configured value is the last accepted count; the query-string
cases need `maxQueryStringLength` raised or System.Web's length check refuses
them first; the urlencoded parser catches everything and rethrows one "not
valid" `HttpException`, so only its inner exception says the limit was the
reason — and form keys written without `=` are all added under one null name,
so a naive over-limit body can count as a single key.

## Explicitly out

- Client certificates, which remain in
  [deferred request surfaces](deferred-request-surfaces.md).
