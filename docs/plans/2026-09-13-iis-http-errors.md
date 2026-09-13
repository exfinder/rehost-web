# IIS httpErrors: File and Redirect rows, existingResponse, the skip flag

## Objective

Honor `system.webServer/httpErrors` the way IIS's custom-error module did,
measured in [readings HE1-HE27](../research/iis-http-errors-readings.md),
without `ExecuteURL`. Today the section is tolerated and ignored, so an
application's own 4xx/5xx body always reaches the client, which is IIS's
`PassThrough` shape rather than its default; and the directory 403 has no
body.

## Decisions

- `errorMode` (`DetailedLocalOnly`, `Custom`, `Detailed`), `existingResponse`
  (`Auto`, `PassThrough`, `Replace`), `defaultResponseMode`, and `<error>`
  rows in `File` and `Redirect` mode are honored. `TrySkipIisCustomErrors`
  is honored under `Auto` and ignored under `Replace` (HE6, HE10).
- Refused at activation, naming the file and element: any row in
  `ExecuteURL` mode, including a row without `responseMode` under
  `defaultResponseMode="ExecuteURL"`; `defaultPath` and
  `allowAbsolutePathsWhenDelegated`, which IIS locks (HE22); a `File` path
  that is absolute (what IIS answers for one is unmeasured); a `Redirect`
  path that is neither site-absolute nor an absolute URL (unmeasured); the
  section in a `<location>` block or a folder `web.config`, root-only like
  `customHeaders` where IIS scoped per path (HE4, HE17); a `File` path whose
  resolved location leaves the application root (unmeasured; validated
  before anything is published).
- Rows key on `(statusCode, subStatusCode)`, `-1` the wildcard, exact
  substatus first (HE20). A duplicate key fails activation (HE21). The
  shipped baseline carries IIS's ten default rows written out, so an
  application's `<remove statusCode="404" subStatusCode="-1" />` has
  something to remove; a row carrying `prefixLanguageFilePath` renders the
  port's built-in body, since the language files have no counterpart (HE2).
- The swap happens once, when the head leaves, whether a mid-request flush or
  the end of the request brings it: what is buffered then is replaced and
  later writes flow through (HE27). The status and every header stay; only
  `Content-Type` and `Content-Length` follow the new entity (HE17, HE24).
- Detailed mode replaces an empty entity only; custom mode replaces whatever
  the application wrote unless it set the skip flag; `PassThrough` sends the
  existing response even when empty (HE5, HE6, HE9).
- A `File` row's type comes from the static MIME map by extension (HE25); a
  missing file falls back to the built-in body silently (HE26). A `Redirect`
  row answers `302 Redirect` with an absolute `Location` and the "Object
  Moved" body, the rewrite step's shape (HE18).
- The runtime writes the detailed body only when the mode says detailed for
  this client; otherwise it sets the status and headers and leaves the
  entity empty for the host, which is how IIS split the work between its
  core and the custom-error module (HE1, HE9). The directory 403 already
  does this.
- Boundaries, named in the compatibility row: the middleware's own 413 and
  the rewrite step's early answers are written past the spool and keep the
  built-in bodies; a Framework page's detailed error and IIS's detailed page
  are both stand-ins, not IIS's bytes; `ExecuteURL` is refused.
- No reading or ledger IDs in code or config comments. No `Co-Authored-By`
  footers.

## Architecture

### Layer 0: configuration (Runtime)

`Compatibility/IisConfig/HttpErrors.cs`, new.

```text
enum HttpErrorMode { DetailedLocalOnly, Custom, Detailed }
enum ExistingResponse { Auto, PassThrough, Replace }
enum HttpErrorRowMode { BuiltIn, File, Redirect }
record HttpErrorRow(int Status, int SubStatus, HttpErrorRowMode Mode,
    string? PhysicalPath, string? ContentType, string? Location)
record HttpErrors(HttpErrorMode ErrorMode, ExistingResponse ExistingResponse,
    IReadOnlyList<HttpErrorRow> Rows)
    HttpErrorRow? Find(int status, int subStatus)   exact, then -1
    bool DetailedFor(bool isLocal)
```

`HttpErrorsSection` reads `/configuration/system.webServer/httpErrors` in
`IisServerConfiguration.ApplyFile` for both files, baseline first, like
`CustomHeaderSection`. Attributes are parsed against their enum names,
case-insensitively, a bad value failing activation naming the file and
attribute. Rows go through `IisCollectionReader.Apply`; the reader gains a
key-reader overload so the composite `statusCode,subStatusCode` key fits
(existing callers unchanged), and the row reader parses both numbers,
`subStatusCode` defaulting to `-1`. `Build(configDirectory, contentTypeOf)`
runs in `Load` after both files: a relative `File` path becomes the absolute
path under the application root, its type from the static map already
loaded, and a `Redirect` path is kept as written. Refusals by XPath, each
naming `configPath` and the element: `//location//httpErrors`,
`httpErrors/@defaultPath`, `httpErrors/@allowAbsolutePathsWhenDelegated`,
and `error[@responseMode='ExecuteURL']`. `RefuseBelowTheRoot` beside the
`customHeaders` one in `IisFolderHandlers.Load`.

`IisServerConfiguration.HttpErrors` exposes the record. The baseline gains
`<httpErrors errorMode="DetailedLocalOnly" existingResponse="Auto"
defaultResponseMode="File">` with IIS's ten rows, `prefixLanguageFilePath`
and `path` as in the golden `applicationHost.config`; the three attributes
are the schema's defaults (`IIS_schema.xml`), written out under the
existing "written out so an amendment has something to amend" comment
style. The schema also fixes the ranges the reader enforces: `statusCode`
400-999, `subStatusCode` -1 to 999, both forming the combined key.

`NativeRefusal.Respond` takes the mode from `IisServerConfiguration.Current`
and writes `IisErrorBodies.Refusal` only when `DetailedFor(IsLocal)`;
otherwise status and headers alone, no `Content-Type`. Its four callers and
`ValidatePath` do not change.

### Layer 1: the swap (Hosting)

`ResponseSpool` gains:

- `HasEntity`, true when any segment exists.
- `BeforeHeadCommit`, an `Action<ResponseSpool, HttpContext>?` the spool
  invokes once, at the top of `CommitAsync`, before the head is written.
- `ReplaceEntity(contentType, bytes)`, `ReplaceEntityWithFile(contentType,
  path, length)`, `Redirect(absoluteLocation)`. Each begins with one private
  `DiscardEntity`: requires head open and not faulted (sealed or not),
  disposes every run so its temp file goes, clears the segments, resets the
  current run, removes `Content-Type` from the header list and clears
  `ContentLength`. Then one segment, the new type, the new length.
  `Redirect` also sets `302`/`Redirect` and adds `Location`.

`HttpErrorPages.cs`, new, one static class:

```text
static void Apply(ResponseSpool spool, HttpContext context, HttpErrors config, bool skip)
    status < 400: return
    config.DetailedFor(IsLocal(context)):
        spool.HasEntity ? return : ReplaceEntity(built-in)
    PassThrough: return
    Auto && skip: return
    row = config.Find(status, 0) ?? built-in
    File: file exists ? ReplaceEntityWithFile : ReplaceEntity(built-in)
    Redirect: Redirect(RequestUrls.Absolute(...) or the URL as written)
    BuiltIn: ReplaceEntity("text/html", IisErrorBodies.Refusal(status))
```

`Apply` never throws: the only I/O is a `File.Exists` and a length read,
and a file that vanishes between them is the same `IOException` the
`TransmitFile` path already turns into an aborted connection at commit.
`IsLocal` is loopback or remote equals local, the worker request's own
rule. The port carries no substatus on the spool, so `Find` is asked with
`0`, which the `-1` rows answer; an exact-substatus row therefore never
matches here and the compatibility row says so.

`AspNetCoreWorkerRequest` overrides `TrySkipIisCustomErrors` as an
auto-property, the way it overrides `SupportsWebSocketUpgrade`.
`RehostWebFormsMiddleware` sets `workerRequest.Response.BeforeHeadCommit`
right after constructing the worker request, closing over the activation's
`HttpErrors` and the worker request's flag. `ClassicPipelineActivation`
gains `HttpErrors`; `AddRehostWebForms` passes it.

Nothing under `System.Web.ReferenceSource` changes. `CustomResponseHeaders`
still runs at Kestrel's head write, after the swap, so the rows ride the
custom page as on IIS (HE24). The WebSocket 101 never reaches the hook with
a 4xx.

## Tests

Pragmatic: unit tests for the spool and the decision, one scenario per
row mode on a shared host, nothing a scenario already proves.

- `Runtime.Tests/Compatibility/IisConfig/HttpErrorsTests`: the baseline's
  ten rows load as `BuiltIn`; an application removes one and adds a `File`
  row whose path and type resolve; the four XPath refusals and the
  `ExecuteURL`-by-default refusal, each asserting the file and element in
  the message; a duplicate key refused. Six tests.
- `Hosting.Tests/ResponseSpoolTests`: a body past the threshold swapped
  before commit deletes its temp file and delivers the new bytes; a swap
  keeps `Connection` and `Allow` and replaces `Content-Type`; a swap after
  the head committed throws; `BeforeHeadCommit` runs once across a flush and
  the terminal commit. Four tests.
- `Hosting.Tests/HttpErrorPagesTests` over a spool and `DefaultHttpContext`:
  the HE5/HE6/HE9/HE10 matrix as one theory (mode, existingResponse, body
  present, skip flag → replaced or kept). One theory.
- Scenario on the `body-customerrors` shared host, which gains
  `<httpErrors errorMode="Custom">` with `404` onto `he/err404.json` (`File`)
  and `403` onto `/he/forbidden.htm` (`Redirect`), plus a `he/probe.aspx`
  that sets a status, writes, and optionally sets the skip flag or flushes.
  Its existing claims survive: the managed-500 conversion is a `302`, and
  the 413 is written past the spool. `HttpErrorsOverKestrelTests`:
  - a missing static file answers `404`, `application/json`, the file's
    bytes, with `X-Powered-By` on it;
  - the probe's `404` with a body is replaced, and kept when it sets the
    skip flag;
  - the directory refusal answers `302` to the absolute `Location` with the
    "Object Moved" body and no `customErrors` redirect (the existing
    `DirectoryRefusalOverKestrelTests` body assertion moves here);
  - a page that sets `404`, flushes, then writes more answers the file
    followed by the second write.
  Four tests.

## Sequence

1. Runtime: record, reader, the collection-reader overload, baseline rows,
   `NativeRefusal`. Unit tests green.
2. Hosting: spool members, `HttpErrorPages`, worker-request flag, wiring.
   Unit tests green.
3. Fixture amendment and scenarios. macOS suite green.
4. Docs: compatibility row `httpErrors` to Partial with the boundaries above;
   ledger P104; the open-tenant row in the IIS configuration plan closed and
   D16 noted as closed; the `DirectoryRequests` comment line about the
   pending tenant removed; the fixtures README row for `body-customerrors`.
5. Windows and Linux rounds on the committed head.

## Done when

Every reading the port claims has a test or a named boundary, the three
rounds pass, and a `web.config` carrying `ExecuteURL`, `defaultPath` or a
sub-root section stops the process at start-up naming the file and the row.
