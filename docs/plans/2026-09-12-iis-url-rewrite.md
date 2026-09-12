# IIS URL Rewrite: inbound rules over the imported parser

## Objective

Honor `system.webServer/rewrite` inbound rules the way the IIS URL Rewrite
Module did, measured in [readings UR1-UR55](../research/iis-url-rewrite-readings.md),
using the parser inside `Microsoft.AspNetCore.Rewrite` and none of its
middleware. Today the section activates and every rule is silently ignored;
IIS without the module refuses the whole application (UR1).

## Decisions

- Inbound rules only. Outbound rules, `<serverVariables>`, wildcard syntax,
  `subStatusCode`, duplicate rule names, `<globalRules>`, and server variables
  outside the importer's list fail activation naming the file and element.
- Root `web.config` only. A folder `web.config` or a `<location>` block
  carrying `<rewrite>` fails activation.
- A `Rewrite` target written as site-absolute (`/…`), parent-relative (`..`),
  or with a scheme fails activation. A substitution that produces one at
  request time gets the ordinary 404.
- Importer-internal gaps are accepted and named: patterns see the re-encoded
  path (`%20`, `%C3%A9`), `REQUEST_URI` carries no query, `HTTPS` is upper
  case, `trackAllCaptures` numbering differs, and once a directory-aware file
  provider makes `IsDirectory` true its `IsFile` is true for a folder too.
- The original request's second `LogRequest`/`EndRequest` tail (UR3) is not
  reproduced. The managed pipeline runs once, on the rewritten URL.

## Architecture

Two layers, one seam. Nothing under `System.Web.ReferenceSource` changes.

### Layer 0: configuration (Runtime)

`Compatibility/IisConfig/RewriteSection.cs`, new. One immutable record:
the section's outer XML and the file it came from.

- `RewriteSection.Read(XmlDocument, configPath)` runs in
  `IisServerConfiguration.ApplyFile` for the application file only and
  returns null when the section is absent.
- Preflight is XPath over the document, each failure an
  `InvalidOperationException` naming `configPath` and the element:
  `//location//rewrite`, `rewrite/globalRules`, `rewrite/outboundRules`,
  `rule/serverVariables`, `rule[@patternSyntax='Wildcard']`,
  `action[@subStatusCode]`, repeated `rule/@name`, and
  `action[@type='Rewrite']/@url` that starts with `/`, `..`, or a scheme.
- `IisFolderHandlers.Load` already walks folder `web.config` files; it
  refuses one whose document has `system.webServer/rewrite`.
- `IisServerConfiguration.Rewrite` exposes the record. Published with the
  rest at bootstrap, before Kestrel listens.

Server-variable names are not checked here: the importer throws
`FormatException` for them at load, and Hosting wraps that with the file name.

### Layer 1: the rewrite step (Hosting)

`UrlRewrite/RewriteRules.cs`, new. Built once in `AddRehostWebForms` after
`WebFormsApplication.Initialize`, from `IisServerConfiguration.Current.Rewrite`,
and stored on `ClassicPipelineActivation`. Null when there is no section, so
the middleware's request path stays as it is for every other application.

```text
RewriteRules
  static RewriteRules? Load(RewriteSection?, physicalRootPath)
      RewriteOptions.AddIISUrlRewrite(new StringReader(xml)); wrap the two
      importer exception types with the config file name.
  RewriteOutcome Apply(HttpContext context)
```

`Apply`, in order:

1. Canonical original: `RequestPathCanonicalizer.Canonicalize(PathBase + Path)`
   plus the query. Store `context.Request.Path` as that canonical value so the
   importer and the worker request read one text.
2. First pass, as `ValidatePath` judges it (UR42, UR43): split with
   `RequestPathInfo.Split(verb, canonical)` and ask `HiddenSegments.Refuses`
   and `ForbiddenExtensions.Refuses` about the file path. Refused: return
   `Skipped`; the runtime step answers the 404 as it does today.
3. `ApplyRule` over the rules with one `RewriteContext` per request:
   `HttpContext`, a `DirectoryAwareFileProvider` over the application root,
   `NullLogger`. Stop at the first result that is not `ContinueRules`.
4. `EndResponse` with a `Location`: make it absolute against the request's
   scheme and host (UR6), set the IIS reason phrase for the status, write the
   IIS "Object Moved" body. `EndResponse` with a status the rule set: reason
   phrase from the rule, IIS custom-error body for the status (UR38).
   `EndResponse` with status still 200 and no `Location` is the abort:
   `context.Abort()` (UR5). All three return `Answered`.
5. Path or query changed: if the query starts with the original query
   followed by `&`, move that original part to the end (UR11). Set the
   `X-Original-URL` request header to the original path in `PathString`
   form plus the query, overwriting a client value (UR9, UR34). Return
   `Rewritten(original)`.
6. Otherwise `Unchanged`.

`UrlRewrite/DirectoryAwareFileProvider.cs`, new. Wraps
`PhysicalFileProvider`; `GetFileInfo` answers an `IsDirectory` entry for a
real folder, which is what makes the importer's `IsDirectory` condition true
(readings, Kestrel section).

`RehostWebFormsMiddleware.InvokeAsync`, after the root-escape check:
`Apply` when rules exist; return on `Answered`; hand the outcome to the worker
request.

`AspNetCoreWorkerRequest`, one optional constructor argument, the original
request when rewritten:

- `GetRawUrl` returns the frozen original path and query (UR9).
- `GetServerVariable` gains `IIS_WasUrlRewritten` (`1` when rewritten, else
  null), `UNENCODED_URL` (the raw target), `REQUEST_URI` (original path and
  query), `CACHE_URL` (scheme, host, original). All four sit in the switch
  beside `WEBSOCKET_VERSION`, so they answer without joining `AllKeys` (UR9).
- `HTTP_X_ORIGINAL_URL` needs nothing: the header lookup already serves it.

The IIS custom-error titles and texts move from `NativeRefusal` into a shared
`IisConfig/IisErrorBodies.cs` so the runtime refusal and the early answer
render the same bytes.

### What stays untouched, and why it is correct

- `ValidatePathExecutionStep` judges the rewritten `FilePath`: the second
  filtering pass (UR27) for free.
- `DirectoryRequestExecutionStep` sees `RewrittenUrl == null` and serves the
  default document under the rewritten path (UR30).
- Form actions, `ResolveClientUrl`, relative `Response.Redirect`,
  `customErrors`, Friendly URLs, and the output cache all follow `RawUrl` and
  `Path` (UR12, UR13, UR37, UR40, UR41).

## Tests

Unit, in the folder mirroring the source:

- `Runtime.Tests/Compatibility/IisConfig/RewriteSectionTests`: one case per
  preflight refusal, each asserting the file and element in the message; a
  well-formed section round-trips.
- `Hosting.Tests/UrlRewrite/RewriteRulesTests` over `DefaultHttpContext`:
  rewrite with query order `rule&original`; case-insensitive match; redirect
  with absolute `Location`, reason phrase and body; custom response; abort;
  skipped when the original file path is refused and rewritten when only the
  path info is; `X-Original-URL` overwritten; unknown variable refused at load
  naming the file.
- `Hosting.Tests/UrlRewrite/DirectoryAwareFileProviderTests`: folder, file,
  missing.
- `Hosting.Tests/AspNetCoreWorkerRequestTests`: frozen `RawUrl`; the four
  variables answer and stay out of `AllKeys`.

Scenario, joining the `webserver` fixture's shared host: its `web.config`
already amends `<system.webServer>`, and every rule pattern sits under a `rw/`
prefix so no other class's request on that host is touched.
`RewriteOverKestrelTests`:

- A rewritten page reports `RawUrl` original, `Url`/`Path`/`QueryString`
  rewritten, `X-Original-URL`, `IIS_WasUrlRewritten`, and a form action
  relative to the original (UR9, UR12).
- A rule into `Private`, the segment that fixture hides, gets the 404 from
  the runtime step (UR27).
- A redirect rule answers with the absolute `Location` and no managed event
  (traced token, silence allowed).
- A rule onto a folder holding the fixture's default document serves it
  (UR30).
- A denied original (`/bin/x`) is refused before the rule (UR26).

No differential test: the readings are the oracle and the compatibility row
cites them.

## Sequence

1. Runtime: `RewriteSection`, the `IisServerConfiguration` hook, the folder
   refusal, `IisErrorBodies`. Unit tests green.
2. Hosting: `DirectoryAwareFileProvider`, `RewriteRules`, the middleware and
   worker-request changes. Unit tests green.
3. Fixture amendment and scenarios. macOS suite green.
4. Docs: compatibility row `system.webServer/rewrite` to Partial with the
   boundaries above; ledger P100; backlog item closed; the `webserver` row in
   the fixtures README names the rule set.
5. Windows and Linux rounds on the committed head.

## Done when

Every measured behavior in UR1-UR55 that the port claims has a test or a
named boundary, the three rounds pass, and a `web.config` carrying an
unsupported shape stops the process at start-up with the file and element
in the message.
