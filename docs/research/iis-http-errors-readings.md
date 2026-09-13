# IIS httpErrors readings

Evidence for the `system.webServer/httpErrors` tenant in the
[IIS configuration plan](../follow-ups/iis-integration-plan.md). HE1-HE27 were
observed on full IIS 10 on `winbox`, 2026-09-13.

## Method

One Integrated v4.0 pool, one site on port 8112, one application per case
(`ec` control, `e1`-`e14`). `probe.aspx` took query switches: `st=` set
`Response.StatusCode`, `body=1` wrote a line, `skip=1` set
`TrySkipIisCustomErrors`, `throw=1` threw, `end=1` called `Response.End`.
`err404.aspx` was the `ExecuteURL` target and printed the status it entered
with, `RawUrl`, `Url`, `Path`, the query and the URL server variables;
`err404s.aspx` did the same and set 404 itself. `err.htm`, `err500.htm` and
`def.htm` sat in each application root; `err.htm` also sat at the site root
with different text. `customErrors mode="Off"` unless a row says otherwise.
Local requests used on-box `curl`; remote ones ran from the Mac, which is
what `DetailedLocalOnly` distinguishes.

## Modes and the default rows

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| HE1 | Control, no section; missing static file, local then remote. | Local: the IIS detailed `404.0` page. Remote: `404`, `Content-Type: text/html`, a 103-byte body reading "The resource you are looking for has been removed, had its name changed, or is temporarily unavailable." | The default is `errorMode="DetailedLocalOnly"`: a remote client is on the custom-error path. |
| HE2 | `errorMode="Custom"`, no rows of its own; missing file without and with `Accept-Language: en-US`. | Without: the same 103-byte line. With: the 1245-byte `%SystemDrive%\inetpub\custerr\en-US\404.htm` page, "404 - File or directory not found." | The inherited rows use `prefixLanguageFilePath` plus the client's language; with no language the module sends its built-in one-line text instead. |
| HE3 | `Custom`, inherited rows: directory without a default document, app-set 401, `PUT` to a static file, app-set 500 with a body, the request-filtering 404.8. | 403 and 401: "You do not have permission to view this directory or page." (58 bytes). 405: "The page you are looking for cannot be displayed because an invalid method (HTTP verb) is being used." with `Allow`. 500: "The page cannot be displayed because an internal server error has occurred." 404.8: the 404 line plus `Connection: close`. | One built-in line per status, `text/html`, no charset; the status's own headers stay. |
| HE4 | `<location path="sub">` with a 404 row in the root file; a `folder/web.config` with its own; the root without a section. | `/sub/missing.txt` and `/folder/missing.txt` serve their rows; `/missing.txt` stays detailed. | The section scopes per path, like `customHeaders` (CH17, CH18). |

## What the module replaces

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| HE5 | Detailed mode, local: page sets 404 with a body; without a body; with a body and `TrySkipIisCustomErrors`. | With a body: the page's body, 404. Without: the detailed 404.0 page. Skip: the page's body. | In detailed mode the module replaces an empty entity only. |
| HE6 | Custom mode (`errorMode="Custom"` local, or the default seen remotely): the same three, plus `Response.End` after the body. | With a body, with `End`, without a body: replaced by the row or the built-in line. Skip: the page's body, 404. | In custom mode `existingResponse="Auto"` replaces whatever the application wrote unless it set `TrySkipIisCustomErrors`. |
| HE7 | Custom mode: page sets 500, 403 or 401 with a body. | Each replaced by its row (`ExecuteURL` page, `Redirect`, built-in line). | Every 4xx/5xx the application sets is judged, not 404 alone. |
| HE8 | `customErrors mode="Off"`: unhandled exception; a missing `.aspx`. Under the control, `Custom`, `PassThrough`, and remotely. | ASP.NET's own error page, 500 or 404, every time. | ASP.NET's error rendering sets the skip flag itself; the module leaves it alone. |
| HE9 | `existingResponse="PassThrough"`: missing static file; 404.8; page sets 404 without a body; with a body; exception. | Missing file: 404, no body, no `Content-Type`. 404.8: the same plus `Connection: close`. No body: 404, empty, `Content-Type: text/plain`. Body and exception page: unchanged. | `PassThrough` sends the existing response even when it is empty. |
| HE10 | `existingResponse="Replace"`: page sets 404 with body and skip; exception. | Both replaced: the 404 row's page; the 500 `File` row with 500. | `Replace` overrides the skip flag and ASP.NET's own error page. |
| HE11 | `customErrors mode="On" defaultRedirect="~/cerr.aspx"` with `File` rows: missing `.aspx`; exception; missing static file; page sets 404 with a body. | `.aspx` and exception: `302` to `cerr.aspx?aspxerrorpath=…`. Static and app-set 404: the `File` row. | `customErrors` handles exceptions only; a set status and a native error reach `httpErrors`. |
| HE12 | The same with `redirectMode="ResponseRewrite"`. | `.aspx` and exception: `200` with `cerr.aspx`'s body. App-set 404: the `File` row. | The rewrite answers 200, so `httpErrors` never sees it. |

## Response modes

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| HE13 | `ExecuteURL` to an `.aspx` that leaves the status alone; to one that sets 404; the 500 row to one that sets 200. | `200`; `404`; `200`. The child page entered with `Response.StatusCode` 200. | The child request owns the status; the module resets it to 200 before the child runs. |
| HE14 | What the child page sees for `/e1/missing.txt?q=1&r=2`. | `RawUrl`, `REQUEST_URI`, `CACHE_URL`: the original path and query. `Url`, `Path`, `SCRIPT_NAME`, `URL`, `UNENCODED_URL`: `/e1/err404.aspx?404;http://localhost:8112/e1/missing.txt?q=1&r=2`. `QueryString` holds that as one key. No `HTTP_X_ORIGINAL_URL`, no `ERROR_*` variables. | The child-execution split of URL Rewrite (UR9) with a `<status>;<absolute original>` query instead of a header. |
| HE15 | `ExecuteURL` to a static `.htm`; `HEAD` on it. | `200` with `Last-Modified`, `ETag`, `Accept-Ranges`, the file's bytes; `HEAD` the same head. | A static error page answers 200 because the static handler does. |
| HE16 | `ExecuteURL` path `/err.htm` from an application under `/e6`, the file present at the site root and in the application. | The site-root file. | `ExecuteURL` paths are site-absolute. |
| HE17 | `File` rows with a relative `path`: in the root file; in a `<location path="sub">` of the root file; in `folder/web.config`. | The application-root file; the application-root file; the folder's file. | A relative `File` path resolves against the directory of the `web.config` that declares it. Status and `Connection: close` stay; `Content-Type: text/html`. |
| HE18 | `Redirect` rows: an absolute URL; a site-relative path with a query; the 403.14 directory refusal and an app-set 403. | `302 Redirect`, `Location` as written or made absolute against the request host, `Content-Type: text/html; charset=UTF-8`, the "Document Moved" body. | The same 302 shape as the rewrite module's redirect (UR6). |
| HE19 | `HEAD` on the `ExecuteURL` page; `HEAD` on the built-in 404 line. | `200` with the page's `Content-Length`; `404` with `Content-Length: 103`, no body. | The head is the answer's head. |

## Rows and configuration shape

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| HE20 | Only a `statusCode="404" subStatusCode="8"` row: 404.8; a missing file; an app-set 404. | 404.8: the row. The other two: the inherited default 404 line. | An exact substatus row matches its substatus only; the `-1` row (the default) matches every substatus. |
| HE21 | A second `<error statusCode="404">` without a `<remove>`. | Every erroring request 500.19 `0x800700b7`, "Cannot add duplicate collection entry of type 'error' with combined key attributes 'statusCode, subStatusCode'", from `CustomErrorModule` at `SendResponse`. A 200 page serves. | The key is `(statusCode, subStatusCode)`; the failure is scoped to consumption. |
| HE22 | `defaultPath="def.htm"` in the application file. | Every erroring request 500.19 `0x80070021` "Lock violation"; 200 pages serve. The server file has `lockAttributes="allowAbsolutePathsWhenDelegated,defaultPath"`. | `defaultPath` and `allowAbsolutePathsWhenDelegated` are locked below the server. |
| HE23 | `defaultResponseMode="File"`, `<clear />`, one 404 row with no `responseMode`; then an app-set 500 and a directory 403. | 404: the row's file. 500 and 403: the built-in lines. | `defaultResponseMode` is delegated; `<clear />` drops the inherited file rows and the built-in text remains the floor. |
| HE24 | `Connection: close` and `X-Powered-By` across every shape above. | The request-filtering `Connection: close` survives the custom page; `X-Powered-By` rides every answer, the `ExecuteURL` page included. | The custom page replaces the entity, not the status's headers. |
| HE25 | `File` rows naming `err.json` and `err.txt`. | `Content-Type: application/json` and `text/plain`, the file's bytes, `Content-Length` the file's size. | The file's extension picks the type through the static MIME map. |
| HE26 | A `File` row naming a file that does not exist. | The built-in one-line text for the status; no 500.19, no log line. | A missing file falls back to the built-in text silently. |
| HE27 | A page that sets 404, writes, calls `Response.Flush`, sleeps, then writes more. | `404`, `Transfer-Encoding: chunked`, `Content-Type` the row's `application/json`; the body is the row's file followed by the page's second write. The same with 200 streams both writes untouched. | The module acts once, when the head leaves: it replaces what is buffered at that moment and later writes pass through. |

## Conclusions for the port

- The port ignores the section today. Its native refusals render
  `IisErrorBodies`, whose titles and texts are the `custerr` file's, and gate
  the detail line on `IsLocal`; the directory 403 has no body and is thrown
  into the managed pipeline, where `customErrors` redirects (D16).
- The visible gap for a remote client is HE6 and HE7: on IIS an
  application's own 4xx/5xx body is replaced by the default page unless it
  set `TrySkipIisCustomErrors`; the port keeps the body, which is IIS's
  `PassThrough` shape. `existingResponse` and the skip flag decide this and
  both are cheap to honor at the point the head is written.
- `File` and `Redirect` rows are one file read or one 302 at that same
  point (HE17, HE18, HE25). The moment is the head commit, whether a
  mid-request flush or the end of the request brings it: what is buffered
  then is replaced and what the page writes afterwards flows through (HE27).
  A row whose file is missing falls back to the built-in text (HE26). `ExecuteURL` is a second managed run on the error
  page's URL after the first has finished, with the `<status>;<original>`
  query, `RawUrl` frozen and the status reset to 200 (HE13, HE14); the
  rewrite step's child-execution seam is the nearest counterpart.
- Rows key on `(statusCode, subStatusCode)` with `-1` as the wildcard
  (HE20, HE21); `IisCollectionReader.Apply` gives the `add`/`remove`/`clear`
  and duplicate semantics. `defaultPath` and `allowAbsolutePathsWhenDelegated`
  are locked on IIS and should fail activation (HE22).
- The section scopes per path (HE4, HE17); root-only with a refusal for a
  folder or `<location>` section is the same first cut `customHeaders` took.
- Language-specific `custerr` files (HE2) have no counterpart: the port's
  fixed bodies stand in for both the language file and the built-in line.
