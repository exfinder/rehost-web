# IIS URL Rewrite Module readings

Evidence for the `system.webServer/rewrite` backlog item. UR1-UR25 were observed
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
`allowedServerVariables` for UR25 and removed at teardown.

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
- Boundaries an implementation must name: cross-application targets (UR8),
  `allowedServerVariables` (UR25), outbound rules (UR24), and the `?` wildcard
  (UR21).
