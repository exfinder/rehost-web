# IIS URL Rewrite Module readings

Evidence for the `system.webServer/rewrite` backlog item. UR1-UR41 were observed
on full IIS 10 on `winbox`, 2026-09-12. Tables keep the stimulus, the observable
result and the conclusion; setup, transcripts and teardown are omitted.

## Method

One Integrated v4.0 pool (`rwpool`), one site on port 8112, one application per
case under `C:\Users\sshuser\probes\rw-rig\apps` (`rc` control, `r1`-`r7`).
`probe.aspx` printed `HttpRequest` members, server variables, `ResolveUrl`, a
`<form runat="server">` and a body token; `probe2.aspx` printed
`ResolveClientUrl`, `HtmlAnchor`/`HyperLink` hrefs and ran `Response.Redirect`,
`Server.Transfer`, `Server.Execute` and `Context.RewritePath`. A `Global.asax`
witness appended one line per managed event with a per-context id minted at
`BeginRequest`, so an event on a context that never saw `BeginRequest` reads
`cid=none`. Requests used on-box `curl`; `httpErrors errorMode="Detailed"`.

UR1 ran before the module existed on the box. URL Rewrite Module 2.1
(`rewrite.dll` 7.1.1993.2351, Microsoft's `rewrite_amd64_en-US.msi`) was then
installed and remains on `winbox`. `HTTP_X_BAR` was added to the server-level
`allowedServerVariables` for UR25 and removed at teardown. Round 3 (UR26-UR40)
added applications for request filtering, `customErrors`, `errorMode="Custom"`,
`runAllManagedModulesForAllRequests`, Friendly URLs 1.0.2 from the NuGet
package, and `@ OutputCache`.

The port's own behavior was measured the same day on macOS: the `webserver`
scenario fixture carrying a `<rewrite>` section with a `Redirect` rule on
`/data.probe` and a `<serverVariables><set>` activates, and `/data.probe`
serves its content with status 200. Every rule is silently ignored.

## Presence and registration

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| UR1 | Application `web.config` with a `<rewrite>` section; module not installed. | Every request in that application, page or static, is 500.19 from IIS Web Core at `BeginRequest`, `0x8007000d`, naming the `web.config` with no line number. No managed event fires. A sibling application without the section serves. | An unrecognized `rewrite` section is fatal for the whole application on IIS; the port's silent tolerance is the divergence. |
| UR2 | Install the module. | `<globalModules>` gains `RewriteModule` (`rewrite.dll`) last; `<modules>` gains an unconditioned `RewriteModule` last, after every managed module. `IIS_UrlRewriteModule` = `7,1,1993,2351` on every request and `ServerVariables.Count` is 46 with no rewrite, 48 after one. | Registration is ordinary module registration; the variable count the port asserts (45) is one lower than an IIS with the module. |

## Pipeline shape of a rewritten request

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| UR3 | `Rewrite` to a page (`/r1/clean/5` → `probe.aspx?id=5`). | One managed context runs `BeginRequest`, `PostMapRequestHandler`, `PreRequestHandlerExecute`, `LogRequest`, `EndRequest`, `PreSendRequestHeaders` on the rewritten path with `IIS_WasUrlRewritten=1`. A second context that never saw `BeginRequest` (`cid=none`, empty `Items`, `Handler` null, no rewrite markers) then fires `LogRequest` and `EndRequest` with the original path and query. | The rewrite runs as a child execution; the original request's managed pipeline is only its log/end tail. `Application_BeginRequest` never sees the original URL. |
| UR4 | `Rewrite` to a static file (`/r1/s` → `static.txt`). | The file is served natively (`text/plain`, 17 bytes). Only the original-path `LogRequest` and `EndRequest` pair fires; no managed event runs for the rewritten side. | A rewrite to native content leaves managed code with the tail events only. |
| UR5 | `Redirect`, `CustomResponse`, `AbortRequest`. | No managed `BeginRequest`. `LogRequest` and `EndRequest` fire on the original path with the status the module set: 301/302/307 (plus `PreSendRequestHeaders`), 403, and 200 for the abort. The abort resets the connection (`curl` exit 56). | Native answers at `BeginRequest` still raise the managed log/end tail, as IV17 measured for early ends. |
| UR6 | `Redirect` wire shape. | Relative `new/{R:1}` → `Location: http://localhost:8112/r1/new/thing?k=1` (absolute, application-relative, query appended). Absolute URL with `appendQueryString="false"` → the literal. Status lines: `301 Moved Permanently`, `302 Redirect`, `307 Moved Temporarily`. Body is IIS's ~150-byte `text/html; charset=UTF-8` "Object Moved" document. | Redirect targets are resolved against the application root; the 302 reason phrase is `Redirect`. |
| UR7 | `CustomResponse statusCode="403" subStatusCode="7" statusReason="Nope" statusDescription="Custom body text"`. | `HTTP/1.1 403 Nope`; the body is IIS's detailed error page for 403.7 (RewriteModule, `BeginRequest`, 5097 bytes) with the description inserted after the title. | `statusReason` is the reason phrase; `statusDescription` lands inside the error page, not as a bare body. |
| UR8 | `Rewrite` to a site-absolute path in another application (`/r1/cross` → `/rc/probe.aspx?from=r1`). | The `rc` application runs the request: its `Global.asax`, its configuration, `RawUrl=/r1/cross`, `HTTP_X_ORIGINAL_URL` set. The `r1` application sees only the original-path log/end pair. | A rewrite crosses application boundaries on IIS; the port's one-application process cannot reproduce that. |

## What ASP.NET sees after a rewrite

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| UR9 | Read request members in the rewritten page. | `RawUrl` = original path and query. `Url`, `Path`, `FilePath`, `CurrentExecutionFilePath`, `AppRelativeCurrentExecutionFilePath`, `PhysicalPath`, `QueryString` = rewritten. Server variables `URL`, `SCRIPT_NAME`, `PATH_INFO`, `PATH_TRANSLATED`, `QUERY_STRING`, `HTTP_URL` = rewritten; `REQUEST_URI`, `UNENCODED_URL`, `CACHE_URL`, `HTTP_X_ORIGINAL_URL` = original. `IIS_WasUrlRewritten` reads `1` but is absent from `AllKeys`; `HTTP_X_ORIGINAL_URL` is in `AllKeys` and is `Request.Headers["X-Original-URL"]`. | `RawUrl` is `IIS7WorkerRequest`'s `CACHE_URL`, captured before any rewrite; every path-derived member follows the rewritten URL. |
| UR10 | Encoded input `/r1/enc/a%2Fb%20c%C3%A9` against `^enc/(.*)$`. | The pattern matched the decoded path: `{R:1}` = `a/b cé` (`%2F` included). `UNENCODED_URL` keeps the raw bytes; `HTTP_X_ORIGINAL_URL` = `/r1/enc/a/b%20c%C3%A9` (slash decoded, the rest kept); `RawUrl` fully decoded; `Request.QueryString` re-encoded as `enc=a%2fb+c%c3%a9`. | Matching runs on the decoded path; the three "original URL" sources differ in encoding. |
| UR11 | Query handling. | Default `appendQueryString` yields the rule's query first, then the original (`fixed=1&orig=2`); `false` drops the original. A later rule's `{QUERY_STRING}` condition sees the query an earlier rule set. | Chained rules operate on the substituted URL including its query. |
| UR12 | Render `<form runat="server">` on a rewritten page, then POST to its action. | `action="./5?id=5"`: path relative to the original URL, query from the rewritten URL. The POST re-enters the rule and the page sees `id=5&id=5`. | Framework's form action is `RawUrl`-relative with the rewritten query; duplicated parameters on postback are the IIS behavior, not a port defect to fix. |
| UR13 | URL resolution inside a rewritten page reached as `/r1/p2/plain=1`. | `ResolveUrl("~/")`, `ResolveUrl("other.aspx")` → `/r1/…` (unaffected). `ResolveClientUrl("other.aspx")`, `ResolveClientUrl("~/other.aspx")`, `HtmlAnchor href="rel.aspx"`, `href="~/tilde.aspx"`, `HyperLink NavigateUrl` → `../other.aspx`, `../rel.aspx`, `../tilde.aspx`, `../hl.aspx`: relative to the original URL. `Response.Redirect("other.aspx?r=1")` → `Location: /r1/p2/other.aspx?r=1`; `Response.Redirect("~/other.aspx?r=2")` → `/r1/other.aspx?r=2`. | Client-relative resolution and relative `Response.Redirect` follow `RawUrl`; both fall out of UR9 once `RawUrl` is the original. |
| UR14 | `Server.Transfer("probe.aspx?t=1")`, `Server.Execute("probe.aspx?x=1")`, `Context.RewritePath("~/probe.aspx?rp=done")` from a rewritten page. | All three behave as on an unrewritten page; `RawUrl` stays the original throughout; the transferred page's form action is `../probe.aspx?t=1`. | In-page transfers compose with a module rewrite. |
| UR15 | `Rewrite` to `probe.aspx/{R:1}` and into `sub/probe.aspx`. | `PathInfo=/one/two`. The page under `sub` reads `sub/web.config` (`appSettings` `where=sub`). | Path-info and configuration resolve against the rewritten path. |

## Rule semantics

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| UR16 | Input shape and no-match. | Distributed-rule input is application-relative (`clean/5` for `/r1/clean/5`) and case-insensitive (`/CLEAN/5` matched `^clean/`). No match, `None` action, or a failed condition leaves the URL untouched and the request continues (404.0 from `StaticFile` for a missing path). | Rules see the path below the `web.config` that declares them. |
| UR17 | Chaining and `stopProcessing`. | `chain1`→`chain2`→`probe.aspx?chain=2` in document order with the substituted URL as the next input. `stopProcessing="true"` ends evaluation; the control rule without it let a later rule fire (`late=1&stop=1`). | Document order, substituted input, stop flag as documented. |
| UR18 | Conditions. | `{HTTP_HOST}` pattern `^foo\.` matched `Host: foo.localhost:8112`; `{C:0}` = `foo.`. The `{REQUEST_FILENAME}` IsFile/IsDirectory negated catch-all skipped existing files and directories, including denied ones (`App_Data/…`, `web.config` stay 404 from request filtering), and rewrote a missing multi-segment path to `probe.aspx?route=some/route/here&y=2`. | Condition inputs and back-references behave as documented; existing-but-denied files count as files. |
| UR19 | `{ToLower:}` and `redirectType`. | `upper/AbC/Def?Q=Z` → `307 Moved Temporarily`, `Location: …/r1/lower/abc/def?Q=Z` (query untouched by the function). | Functions apply to the substitution only. |
| UR20 | Rewrite map keyed on `{REQUEST_URI}`. | The key is the site-absolute `/r1/diag`; the value `probe.aspx?map=diag` resolves application-relative. | Map keys carry the application prefix when keyed on `REQUEST_URI`. |
| UR21 | Wildcard patterns. | `w2/*` matched `w2/mid` (`{R:1}`=`mid`); `w3/*/*.txt` matched `w3/mid/x.txt` (`mid`, `x`). `w/*/x.???` and `*/x.???` did not match `w/mid/x.txt` (404 `StaticFile`). | `*` works across one segment; `?` did not match here. Recorded, not explained. |
| UR22 | HTTPS and trailing-slash redirects in an application under `/r3`. | `https://{HTTP_HOST}/{R:1}` → `Location: https://localhost:8112/probe.aspx?a=1`: `{R:1}` is application-relative, so the `/r3` prefix is lost. Slash rule: `about` → `301 …/r3/about/`, `about?x=1` → `…/about/?x=1`; an existing directory `sub` → IIS's own `301 sub/` (same without the rule); `sub/` → 403.14 `DirectoryListingModule`. | The common HTTPS rule is only correct for a site-root application. |
| UR23 | Folder and `<location>` scope. | A rule in `sub/web.config` sees input relative to `sub` (`local/q`) and its target resolves under `sub` (`/r1/sub/probe.aspx`), case-insensitively through the folder (`/Sub/Local/Q`). A rule in `<location path="sub">` of the application `web.config` sees input relative to the application (`^local/` never matched, `^sub/local2/` did) and its target resolves at the application root (`/r7/probe.aspx?loc2=x`). | Folder files scope input and target; `<location>` scopes neither, against the documented behavior. |
| UR24 | Outbound rules. | `<match filterByTags="None" pattern="REWRITE_ME">` under a `{RESPONSE_CONTENT_TYPE}` `^text/` precondition replaced the token in a page body and `Content-Length` grew with it (1593→1596). `<match serverVariable="RESPONSE_X_Out">` added the `X-Out` header on the page and on a static file. | Outbound rules edit managed and native responses alike, with length recomputed. |
| UR25 | `<serverVariables><set name="HTTP_X_FOO">` without and `HTTP_X_BAR` with a server-level `allowedServerVariables` entry. | Without: that URL answers `500 URL Rewrite Module Error.` (500.50, RewriteModule, `BeginRequest`, `0x80070005`, "The server variable "HTTP_X_FOO" is not allowed to be set"); other URLs in the application serve. With: the page reads `Request.Headers["X-Bar"]` and `ServerVariables["HTTP_X_BAR"]` = `bar-set`. | Setting a variable is gated by server-level configuration the application cannot carry, and fails per request, not per application. |

## Order against the rest of IIS

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| UR26 | Original path request filtering denies: `/bin/foo.dll`, `/foo.cs`, `/App_Data/x`, each with a rule rewriting it to `probe.aspx`. | 404.8 / 404.7 / 404.8 from `RequestFilteringModule` at `BeginRequest`, requested URL the original, no rewrite marker. The `bin` and `.cs` cases raise no managed event at all; the `App_Data` case raises the log/end tail. | Request filtering runs on the original URL before any rule; a denied original never reaches the rules. |
| UR27 | Rules rewriting an allowed path into denied content: `/h` → `App_Data/secret.txt`, `/cfg` → `web.config`. | 404.8 from `RequestFilteringModule`, requested URL the rewritten one (`/r8/App_Data/secret.txt`, `/r8/web.config`); only the original-path log/end tail fires. | Filtering runs again on the rewritten URL; a rule cannot expose hidden content. |
| UR28 | Dot segments in the original: `/dots/a/../b` and `/dots/a/%2e%2e/b` (sent verbatim). | Both reach the rule as `dots/b`; `RawUrl` and `HTTP_X_ORIGINAL_URL` are `/r8/dots/b`; `UNENCODED_URL` keeps the raw form, with the escape upper-cased (`%2E%2E`). | Canonicalization precedes rule matching; only `UNENCODED_URL` remembers the wire form. |
| UR29 | Rule `^dir$` → `probe.aspx?dir=1` where `dir/` is a real folder; `/DIR` too. | The rule wins (200, `RawUrl=/r8/dir`); IIS's slash redirect never runs. `/dir/` without a rule is 403.14. | A rule at `BeginRequest` outranks the directory courtesy redirect. |
| UR30 | Rules targeting a folder: `^todir$` → `sub/` with `defaultDocument` `probe.aspx`; `^todir2$` → `sub`. | `sub/` serves the default document: `Path=/r8/sub/probe.aspx`, `RawUrl=/r8/todir`, `HTTP_X_ORIGINAL_URL=/r8/todir`, but `IIS_WasUrlRewritten` is empty and `UNENCODED_URL` is `/r8/sub/probe.aspx`. `sub` answers IIS's own `301 http://localhost:8112/r8/sub/`, exposing the rewritten path in `Location`. | Default-document dispatch re-executes on top of the rewrite and drops two of its markers; the slash redirect leaks the rewritten name. |
| UR31 | Rule `^$` on a request for the application root `/r8/`. | Matches; `RawUrl=/r8/`, form action `./?root=1`. | The root request presents an empty input. |
| UR32 | Rewrite to another host, `http://example.invalid/x`. | 404.4 from IIS Web Core at `MapRequestHandler`, `0x8007007b`, "does not have a handler". | Without ARR a foreign-host rewrite is a mapping failure, not a proxy. |
| UR33 | Rewrite to `../rc/probe.aspx?up=1` from `/r8`. | Runs in the `rc` application, as UR8. | Parent-relative targets escape the application. |
| UR34 | Client sends `X-Original-URL: /spoof` on an unrewritten and on a rewritten request; client sends `IIS_WasUrlRewritten: 1`. | Unrewritten: `RawUrl` and every path member are untouched; `Request.Headers["X-Original-URL"]` and `HTTP_X_ORIGINAL_URL` read `/spoof`. Rewritten: the module overwrites both with the true original. The `IIS_WasUrlRewritten` header never becomes the server variable. | `RawUrl` comes from `CACHE_URL`, never from the header; a port must not derive the original from client input, and must overwrite the header on a rewrite. |
| UR35 | Redirect rules in `sub/web.config`: `there` and `/there2`. | `302 http://localhost:8112/r8/sub/there` and `302 http://localhost:8112/there2`. | Relative redirect targets resolve under the declaring folder; a leading slash is site-absolute. |
| UR36 | Substitution strings with a space and with `{UrlEncode:x y/z}`. | `Url` carries the space verbatim (`?s=a b`) and `QueryString` reads `s=a+b`. `UrlEncode` encoded only the slash: `?q=x y%2Fz`. | The module does not encode substitutions itself; `UrlEncode` leaves spaces alone. |

## Application behavior on a rewritten request

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| UR37 | `customErrors mode="On"` with a 404 row and `defaultRedirect`; rewrite `/missing` → `missing.aspx` and `/boom` → a throwing page. | Both redirect to `/r9/err.aspx?aspxerrorpath=/r9/missing.aspx` and `…=/r9/boom.aspx`, byte-identical to the unrewritten controls. `Application_Error` fires on the rewritten context with `RawUrl` original. | `aspxerrorpath` carries the rewritten path; error handling is unaware of the rewrite. |
| UR38 | `errorMode="Custom"` with `CustomResponse` 403.7 and 404. | `HTTP/1.1 403 Nope` with IIS's 58-byte custom error body "You do not have permission to view this directory or page."; the 404 case gets the 103-byte "removed, had its name changed" body, identical to an ordinary missing file. `statusDescription` appears nowhere. | A remote client sees the reason phrase and IIS's stock body; the description is local-detail only. |
| UR39 | `runAllManagedModulesForAllRequests="true"`, rewrite `/s` → `static.txt`. | The rewritten context now runs the managed events (`BeginRequest` … `PreSendRequestHeaders`, `Handler` null) with `IIS_WasUrlRewritten=1`, then the original-path tail. Same shape as an unrewritten static request under the flag. | The flag governs the rewritten side exactly as it governs any static request. |
| UR40 | Friendly URLs 1.0.2 with `AutoRedirectMode.Permanent`; rewrite `/fu/(.*)` → `{R:1}` and `/legacy` → `about.aspx`. | `/fu/about` and `/fu/about/seg1/seg2?x=1` route to `about.aspx` with segments `seg1|seg2`. `/legacy` and `/fu/about.aspx` serve `about.aspx` with no 301, while the direct `/about.aspx` is 301 to `/about`. `FriendlyUrl.Href("~/about","x")` returns `/r12/fu/about/x` on the rewritten request and `/r12/about/x` otherwise. | Routing consumes the rewritten path; the friendly redirect and `Href` read the original, so a rewritten `.aspx` is not redirected. |
| UR41 | `@ OutputCache Duration="60"` page behind rules `c1`, `c2`, `c3` (`?v=3`) with `VaryByParam="none"`, and `d1`/`d2` sharing one rewritten query with `VaryByParam="*"`. | Every variant served the entry the first original filled: `RawUrl=/r13/c1`, form action `./c1`, identical ticks, for `/c2`, `/cached.aspx` and `/c3`; `/d2` served `/d1`'s page. | The managed output cache keys on the rewritten path and query only; the first original's form action is served to every other original. |

## The ASP.NET Core importer, measured against the readings

`Microsoft.AspNetCore.Rewrite` 10.0.10 (shared framework) was probed on macOS
the same day: `RewriteOptions.AddIISUrlRewrite(TextReader)` parsed each
document, and the resulting `IRule`s were applied to a `DefaultHttpContext`
through a hand-built `RewriteContext`, with no `RewriteMiddleware`. The
program is a scratch console project, not committed.

Usable without the middleware: `IRule` and `RewriteContext` are public,
`RewriteContext` has a parameterless constructor and public setters for
`HttpContext`, `StaticFileProvider`, `Logger` and `Result`; parsing is eager,
so a bad document throws at load with `InvalidUrlRewriteFormatException`
(line and position) or `FormatException`. The rule type itself is internal.
Input is `Request.Path` with `PathBase` and the leading slash removed, so a
rule sees the same application-relative text IIS gives it (UR16). Regex
options are `IgnoreCase | Compiled | CultureInvariant` with a one-second
timeout; `ignoreCase="false"` drops the flag.

| Feature | Importer | IIS reading |
|---|---|---|
| Server variables | Fixed list of 17: `HTTP_HOST`, `HTTPS`, `QUERY_STRING`, `REQUEST_URI`, `REQUEST_FILENAME`, `REQUEST_METHOD`, `HTTP_USER_AGENT`, `HTTP_COOKIE`, `HTTP_REFERER`, `HTTP_CONNECTION`, `HTTP_URL`, `SERVER_NAME`, `REMOTE_ADDR`, `REMOTE_PORT`, `LOCAL_ADDR`, `CONTENT_TYPE`, `CONTENT_LENGTH`. Any other name, including every other `HTTP_*` header and `HTTP_X_FORWARDED_PROTO`, `SERVER_PORT`, `PATH_INFO`, `UNENCODED_URL`, `HTTP_X_ORIGINAL_URL`, throws `FormatException: Unrecognized parameter type` at load. | Any request header is a variable (UR18, UR25). |
| `REQUEST_URI`, `HTTP_URL` | Path only: no `PathBase`, no query. | Site-absolute path with query (UR9, UR20); a rewrite map keyed on `{REQUEST_URI}` therefore misses every keyed query. |
| `HTTPS` | `ON` / `OFF`. | `on` / `off` (UR9). Equal under the default `ignoreCase`. |
| Query append order | Original first, then the rule's: `/clean/5?extra=9` → `?extra=9&id=5`. | Rule's first: `id=5&extra=9` (UR11). |
| Encoded path input | The regex sees `Request.Path` as given; on `DefaultHttpContext` that is the encoded text (`enc/a%2Fb%20c`). What Kestrel hands the adapter is the port's canonical-request question. | Matching runs on the decoded path (UR10). |
| `Redirect` | `Location` is a root-relative path with `PathBase` prepended (`/r1/new/thing?k=1`); an absolute URL passes through; 301/302/303/307 and numeric codes including 308; `PermanentRedirect` is refused. | Absolute `Location` (UR6); 308 unsupported. |
| `CustomResponse` | `subStatusCode` throws `NotSupportedException` at load; without it the status, reason and description load. | 403.7 with reason phrase and description inside the error page (UR7, UR38). |
| `AbortRequest` | `Result = EndResponse`, status left at 200, nothing closes the connection. | Connection reset (UR5). |
| `IsDirectory` | `/sub` and `/` were not recognized as directories over a `PhysicalFileProvider` holding `sub/`; the catch-all rewrote both. | Both skipped as directories (UR18, UR22). |
| `{C:n}` | Last matched condition unless `trackAllCaptures="true"`, then cumulative. | `{C:0}` = last condition's match (UR18); the rest unmeasured. |
| Wildcard syntax | `NotSupportedException` at load. | Partially working (UR21). |
| `<outboundRules>` | Loads and is silently dropped (zero rules). | Applied (UR24). |
| `<serverVariables><set>` | Loads and is silently dropped; nothing is set. | Applied under an allow list, else 500.50 (UR25). |
| `<globalRules>`, `<location path>`, whole `web.config` as input | All load; `<location>`'s path is ignored and the rules apply application-wide. | `<location>` scoping is application-wide too (UR23). |
| `enabled="false"`, duplicate names, `<clear/>` | Dropped; accepted; accepted. | Duplicate names unmeasured. |
| Functions | `ToLower`, `UrlEncode`, `UrlDecode`; `UrlEncode` double-encodes an already encoded input. | `UrlEncode` left a space unencoded (UR36). |
| Back-references | `{R:0}`-`{R:9}`, `{C:0}`-`{C:9}`; `{R:10}` throws at load. | Documented range. |
| Rewrite maps | Case-insensitive keys, `defaultValue` honored, miss leaves the path. | Case unmeasured. |

Silent drops are the importer's own least-astonishment failures: a port that
adopts it must refuse `<outboundRules>` and `<serverVariables>` itself before
calling it, and must own the abort, the query order, the absolute `Location`,
the directory test and the decoded input if IIS parity is the claim.

## Conclusions for the port

- Today the port and IIS-without-the-module disagree in opposite directions:
  IIS refuses the whole application (UR1), the port serves it with every rule
  ignored. Either honoring the section or refusing it at activation is closer
  to IIS than the current silence.
- Honoring it needs `RawUrl` to stay the original while every other path member
  follows the rewritten URL (UR9). Form actions, client-relative hrefs and
  relative `Response.Redirect` then need nothing extra (UR12, UR13).
- The managed pipeline runs once, on the rewritten URL, plus an original-path
  `LogRequest`/`EndRequest` tail on a separate context (UR3-UR5). A port that
  rewrites before the managed pipeline and raises only the rewritten context
  matches everything an application can observe except that tail.
- Rule evaluation sits after canonicalization and after request filtering of
  the original URL, and filtering runs again on the result (UR26-UR28). The
  port's existing canonical-request boundary is the right input.
- `RawUrl` must come from the request line the host received, never from an
  `X-Original-URL` header, and a rewrite must overwrite that header (UR34).
- Boundaries an implementation must name: cross-application and parent-relative
  targets (UR8, UR33), foreign hosts (UR32), `allowedServerVariables` (UR25),
  outbound rules (UR24), and the `?` wildcard (UR21).
- The importer covers the rule vocabulary real configs use, but five of its
  behaviors differ from the readings and two sections drop silently (table
  above); the variable allow-list is the one that refuses real configs.
- Consequences that need no port work once `RawUrl` is right: form actions,
  client-relative links, `customErrors`, Friendly URLs, and the output-cache
  collision (UR12, UR13, UR37, UR40, UR41).
