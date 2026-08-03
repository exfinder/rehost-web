# Postback and form parsing

Status: implemented and verified on macOS `arm64` and Windows `x64`; the
Framework oracle session is outstanding. Scope: urlencoded form parsing,
view state, control state, a postback that changes rendered output, and
read-only multipart. Sits on the
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

## Outstanding

Both need the Windows `x64` host. The cross-runtime payload half of the first
item now has measured answers recorded in
[mixed farm and incremental migration](mixed-farm-incremental-migration.md);
what remains is making them a standing gate rather than an observation.

- One oracle session comparing status, headers, markup excluding the three crypto
  fields, and the postback event sequence, plus a Framework-captured `__VIEWSTATE`
  posted to the port and the reverse.
- Confirm ledger P43's narrowed claim against .NET Framework 4.8.1 rather than
  against Reference Source. Two readings settle it: whether
  `HKLM\SOFTWARE\Microsoft\.NETFramework\v4.0.30319\AspNetEnforceViewStateMac`
  is set on that host, and whether a page with a server form renders
  `__VIEWSTATEGENERATOR` there. It also decides which case the oracle would run
  under if a fixture ever stopped pinning `aspnet:AllowInsecureDeserialization`.

## Explicitly out

- `HttpPostedFile.SaveAs` and `RequireRootedSaveAsPath`. `Path.IsPathRooted`
  disagrees across operating systems, so that branch needs coverage on the
  platform that triggers it and is its own story.
- `MaxHttpCollectionKeys` rejection.
- Client certificates, which remain in
  [deferred request surfaces](deferred-request-surfaces.md).
