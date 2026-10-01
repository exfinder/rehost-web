# IIS request filtering limits readings

Evidence for the `security/requestFiltering` limits tenant in the
[IIS configuration plan](../follow-ups/iis-integration-plan.md): `requestLimits`,
`verbs`, `headerLimits`, and the sequence and character switches. RL1-RL24 were
observed on full IIS 10 on `winbox`, 2026-09-13.

## Method

One Integrated v4.0 pool, one site on port 8112, one application per case
(`lc` control, `l1`-`l8`). `probe.aspx` read the whole entity and printed the
verb, `ContentLength`, bytes read, `RawUrl.Length`, the query length and
`PathInfo`. Bodies of 500, 1500 and 5000 bytes came from files; long URLs were
built from segments so http.sys's own 260-byte segment cap stayed out of the
way except where it is the reading. Requests used on-box `curl`;
`httpErrors errorMode="Detailed"`; `customErrors mode="Off"`.

## Content length

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| RL1 | Control, no section; POST 1500 bytes. | 200, all 1500 read. | IIS's default `maxAllowedContentLength` is 30,000,000 bytes. |
| RL2 | `maxAllowedContentLength="1000"`; POST 500 and 1500 bytes with `Content-Length`. | 500: 200. 1500: `413 Request Entity Too Large`, 413.1 from `RequestFilteringModule` at `BeginRequest`, `Connection: close`, IIS error page. | The declared length is judged before any managed code; the answer closes the connection. |
| RL3 | The same 1500 bytes with `Expect: 100-continue`. | 413.1 straight away, no `100 Continue`. | The refusal comes before the entity is asked for. |
| RL4 | The same 1500 bytes, `Transfer-Encoding: chunked`, no `Content-Length`. | 200, `ContentLength=0`, 1500 bytes read. | The limit is a `Content-Length` check only; a chunked entity is never measured against it. |
| RL5 | POST 1500 bytes to a static file. | 413.1. | The check runs for every handler, native ones included. |
| RL6 | `maxAllowedContentLength="1000000"` with `<httpRuntime maxRequestLength="1">` (1 KB); POST 5000 bytes. | Managed 500 "Maximum request length exceeded." from ASP.NET. | The managed limit answers when it is the smaller one, as its own error page. |
| RL7 | `maxAllowedContentLength="1000"` with `maxRequestLength="1"`; POST 1500 and 500. | 1500: 413.1 native. 500: 200. | Whichever limit is smaller answers, each in its own shape. |

## URL and query length

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| RL8 | `maxUrl="40"`; a URL no handler claims (`/l1/nosuch/…`) of 41, 100 and 300 bytes. | 404.14 from `RequestFilteringModule` at `BeginRequest`, `Connection: close`. | `maxUrl` is enforced against the request path in bytes. |
| RL9 | `maxUrl="40"`; `/l1/probe.aspx/` plus path info to 41, 60 and 100 bytes. | 200, `RawUrl.Length` 41, 60, 100, `PathInfo` carrying the rest. | Path info after a script the handler map claims is not counted, the same split `fileExtensions` uses (UR42, UR43). |
| RL10 | Control (`maxUrl` 4096): unclaimed paths of 4097, 4200 and 5000 bytes in 200-byte segments. | 404.14. A 4000-byte one is 400 from ASP.NET instead (RL13). | The default is 4096 bytes and it is judged before ASP.NET. |
| RL11 | `maxQueryString="20"`; queries of 20 and 21 bytes after `?`. | 20: 200. 21: 404.15 from `RequestFilteringModule`, `Connection: close`; same on HEAD. | The query is judged whole, in bytes, excluding `?`. |
| RL12 | Control (`maxQueryString` 2048); a 2052-byte query. | 404.15. | The default is 2048 bytes. |
| RL13 | A 300-byte path the page handler claims (`probe.aspx/…` in 100-byte segments), and a 4000-byte extensionless path. | ASP.NET 400: "The length of the URL for this request exceeds the configured maxUrlLength value." | Once past request filtering, `<httpRuntime maxUrlLength>` (260 by default) answers as a managed 400. |
| RL14 | A single path segment of 500 bytes; a 20,000-byte URL. | `400 Bad Request` "The request URL is invalid." from http.sys; `414 Request-URI Too Long` from http.sys. | http.sys caps a segment at 260 bytes and the URL at 16 KB before IIS runs; neither is request filtering. |

## Verbs

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| RL15 | Control: PUT and DELETE to a page; TRACE. | `405 Method Not Allowed` from `StaticFileModule` with `Allow: GET, HEAD, OPTIONS, TRACE`; TRACE is `501 Not Implemented` from `ProtocolSupportModule` (MH38). | Without a verbs section an unmapped verb falls through the handler map to the static handler. |
| RL16 | `<verbs allowUnlisted="false">` with GET and POST allowed: PUT, OPTIONS, HEAD, and lower-case `get`. | All 404.6 from `RequestFilteringModule` at `BeginRequest`, `Connection: close`; POST is 200. | The allow list is exact and case-sensitive; HEAD and OPTIONS need their own rows. |
| RL17 | `<verbs>` denying DELETE and TRACE, unlisted allowed: DELETE, TRACE, PUT. | DELETE and TRACE 404.6; PUT stays the handler's 405. | A deny row answers before the handler map; an unlisted verb keeps its ordinary fate. |
| RL18 | POST to a static file. | 405 from `StaticFileModule`, `Allow: GET, HEAD, OPTIONS, TRACE`. | The static handler's verb list, not request filtering. |

## Headers, characters and sequences

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| RL19 | `<headerLimits><add header="X-Big" sizeLimit="10" />`; the header at 5 and 24 bytes. | 5: 200. 24: `431 Request Header Fields Too Large` from `RequestFilteringModule`, `Connection: close`. | Per-header size limits answer 431. |
| RL20 | `allowHighBitCharacters="false"`; `/caf%C3%A9.txt`. | 404.12. | The switch refuses non-ASCII bytes in the URL. |
| RL21 | `<denyUrlSequences><add sequence="_vti_" />`; `/_vti_bin/probe.aspx`. | 404.5. | URL sequences are refused before the handler map. |
| RL22 | `<denyQueryStringSequences><add sequence="&lt;script" />`; `?x=<script>` raw and `%3Cscript%3E` encoded. | 404.18 for both. | Query sequences are matched after decoding. |
| RL23 | `allowDoubleEscaping="true"`; `/sp%2520ace/probe.aspx`. | Managed 500 "A potentially dangerous Request.Path value was detected from the client (%)". With the default `false` the same request is 404.11 (UR49). | Allowing double escaping only moves the refusal to ASP.NET's request validation. |
| RL24 | The verbs, limits and sequence refusals above; which pipeline saw them. | Every one is `RequestFilteringModule` at `BeginRequest` with `Connection: close` and the IIS error page; no managed event fires. | The whole family is a native early answer with one shape, like hidden segments (P59). |

## Conclusions for the port

- Every limit and switch in this family answers at `BeginRequest`, natively,
  with `Connection: close` and a `404.x`, `413.1` or `431` page (RL24). The
  port's `NativeRefusal` already renders that family; the new statuses need
  their titles and texts, and the connection close.
- `maxAllowedContentLength` judges the declared `Content-Length` only (RL2-RL5).
  The port's middleware already refuses a declared length above Kestrel's limit
  with a 413 before the pipeline; feeding it the configured value is the
  translation. A chunked entity is not measured by IIS (RL4), so the port must
  not measure it either, and the managed `maxRequestLength` keeps its own path
  (RL6, RL7).
- `maxUrl` and `fileExtensions` share the handler split (RL9): the check sits
  where `ValidatePath` judges the script file, or beside the rewrite step's
  first pass, on the same `RequestPathInfo.Split`. `maxQueryString` judges the
  raw query bytes (RL11).
- http.sys's own 260-byte segment and 16 KB URL caps (RL14) and ASP.NET's
  `maxUrlLength` (RL13) are separate layers; Kestrel's request-line limit is 8
  KB by default and is the nearest counterpart to the 16 KB cap.
- Verb filtering is case-sensitive and exact (RL16); a deny row answers 404.6
  where the handler map would have answered 405 (RL17).
