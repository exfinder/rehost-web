# IIS request filtering limits

## Objective

Honor the four rows of `security/requestFiltering` that real configs carry,
measured in [readings RL1-RL24](../research/iis-request-limits-readings.md):
`requestLimits maxAllowedContentLength`, `maxUrl`, `maxQueryString`, and the
`verbs` collection. Today all four are silently ignored; the shipped baseline
already carries IIS's defaults for them.

## Decisions

- `maxAllowedContentLength` replaces Kestrel's default as the declared-length
  limit the middleware already refuses before the pipeline (RL2, RL3, RL5).
  The answer gains IIS's 413 body and `Connection: close`. A chunked entity is
  not measured, as on IIS (RL4); `httpRuntime maxRequestLength` keeps its own
  managed path (RL6, RL7). A consumer's explicit Kestrel body limit is not
  consulted: the application's value is the limit, as on IIS.
- `maxUrl` judges the script path the handler map claims, in bytes; path info
  past a claimed script is not counted (RL8, RL9). `maxQueryString` judges the
  raw query bytes after `?` (RL11). Both answer 404 through `NativeRefusal`.
- `verbs`: exact, case-sensitive match against the rows; `allowUnlisted`
  decides the rest (RL16, RL17). A refused verb answers 404 through
  `NativeRefusal`, ahead of the handler map's own 405.
- Root `web.config` only, as with the other sections. `headerLimits`, the
  character switches, the sequence lists and `filteringRules` stay unhonored
  and are named in the compatibility row. http.sys's segment and 16 KB caps
  (RL14) have no counterpart and are named too.
- No reading or ledger IDs in code or config comments.

## Architecture

### Runtime

`IisConfig/RequestLimits.cs`, new: one record with the three numbers, the
verb rows and `AllowUnlistedVerbs`. Read in `IisServerConfiguration.ApplyFile`
for both files, baseline first, from
`security/requestFiltering/requestLimits` (attributes) and `.../verbs`
(`IisCollectionReader.Apply` with `verb` as the key and `allowed` as the
value, `allowUnlisted` through `OptionalBoolean`). A non-numeric or negative
attribute fails activation naming the file and attribute. Exposed as
`IisServerConfiguration.RequestLimits`.

`Compatibility/RequestFiltering.cs`, new: one static predicate,
`Refusal? Judge(verb, filePath, rawQuery)`, returning the substatus text for
the first of verb, URL length, query length that fails, or null. It sits
beside `HiddenSegments` and `ForbiddenExtensions`.

`HttpContext.ValidatePath` calls it after the two existing checks and answers
with `NativeRefusal.Respond(this, 404, text)`. `IisErrorBodies` gains the 404.6,
404.14, 404.15 and 413 titles and texts from the readings.

### Hosting

`RewriteRules.ApplyAsync`'s first pass calls the same predicate after the two
it already calls, so the original URL is judged before any rule (RL24, same
order as filtering).

`RehostWebFormsMiddleware.ExceedsHostBodyLimit` compares the declared length
to `RequestLimits.MaxAllowedContentLength` and the 413 answer writes
`IisErrorBodies.Refusal(413)` with `Connection: close`.

Nothing under `System.Web.ReferenceSource` changes except the one call in
`ValidatePath`, which already carries the port's request-filtering block.

## Tests

Pragmatic: one scenario per reading family on the `webserver` shared host, no
new process, no unit test for what a scenario proves. The fixture's values sit
above what its other classes use: `maxAllowedContentLength="1000"`,
`maxUrl="300"`, `maxQueryString="200"`, `verbs` denying `DELETE` and `TRACE`
with unlisted allowed.

- `RequestLimitsOverKestrelTests`: a 1500-byte POST is 413 with the IIS body
  and `Connection: close` while 500 bytes and a chunked 1500 serve; a 301-byte
  unclaimed path is 404 while a page plus 300 bytes of path info serves; a
  200-byte query serves and 201 is 404; `DELETE` is 404 while `POST` serves.
  Four tests, each asserting the substatus text.
- `Runtime.Tests/Compatibility/IisConfig/RequestLimitsTests`: the baseline
  defaults load; an application amends them; a bad number fails activation
  naming the file. Three tests.

Total runtime added to the suites: seconds, not minutes.

## Sequence

1. Runtime: record, reader, predicate, `ValidatePath` call, error bodies.
2. Hosting: the first-pass call and the 413.
3. Fixture amendment and scenarios. macOS suite green.
4. Docs: compatibility row for request-filtering limits to Partial with the
   boundaries above; ledger P102; the tenant row in the IIS configuration
   plan; the `webserver` row in the fixtures README.
5. Linux round on the committed head.
