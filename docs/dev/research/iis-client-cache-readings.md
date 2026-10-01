# IIS staticContent/clientCache readings

Evidence for the `staticContent/clientCache` tenant in the
[IIS configuration plan](../follow-ups/iis-integration-plan.md). CC1-CC15 were
observed on full IIS 10 on `winbox`, 2026-09-13.

## Method

One Integrated v4.0 pool, one site on port 8112, one application per case
(`cc` control, `c1`-`c12`). Each application held `static.txt`, `data.json`,
`probe.aspx`, `sub/static.txt`, `sub2/index.htm` and `folder/static.txt`.
Requests used on-box `curl`; `httpErrors` default, `customErrors mode="Off"`.
`Server`, `X-Powered-By`, `Accept-Ranges` and `Content-Length` were on every
answer and are not repeated below.

## Modes

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| CC1 | Control, no `clientCache`: a static file, a page, a default document. | Static file and default document: `Last-Modified`, `ETag`, no `Cache-Control`, no `Expires`. Page: `Cache-Control: private` from ASP.NET. | The default `cacheControlMode="NoControl"` sends nothing; the native static module owns the header. |
| CC2 | `cacheControlMode="UseMaxAge" cacheControlMaxAge="1.00:00:00"`: a `.txt`, a `.json`, the default document, `HEAD`. | `Cache-Control: max-age=86400` on all four; `Last-Modified` and `ETag` unchanged. | The span becomes `max-age` in seconds, on every static answer whatever the extension. |
| CC3 | The same on a page and on a missing file. | Page: `Cache-Control: private`. Missing file: the 404 page with `Cache-Control: private`. | Managed answers are untouched. |
| CC4 | `UseMaxAge` with `cacheControlMaxAge="00:00:30" cacheControlCustom="public"`. | `Cache-Control: public,max-age=30`. | `cacheControlCustom` goes first, then `max-age`, joined by a comma with no space. |
| CC5 | `UseMaxAge` with `cacheControlCustom="private"` and an `httpExpires` set too. | `Cache-Control: private,max-age=86400`; no `Expires`. | `httpExpires` is ignored outside `UseExpires`. |
| CC6 | `cacheControlMode="UseExpires" httpExpires="Tue, 19 Jan 2038 03:14:07 GMT"`. | `Expires: Tue, 19 Jan 2038 03:14:07 GMT`; no `Cache-Control`. | `UseExpires` sends the date verbatim and nothing else. |
| CC7 | `UseExpires` with `cacheControlCustom="public"`. | `Cache-Control: public` and the `Expires` date. | The custom text still rides as `Cache-Control`. |
| CC8 | `cacheControlMode="DisableCache"`. | `Cache-Control: no-cache`; no `Expires`. | One fixed word. |
| CC9 | `cacheControlMode="NoControl" cacheControlCustom="public, must-revalidate"`. | `Cache-Control: public, must-revalidate`. | The custom text is sent verbatim on its own. |
| CC10 | `UseMaxAge` with `cacheControlMaxAge="365.00:00:00" setEtag="false"`. | `Cache-Control: max-age=31536000`; no `ETag`; `Last-Modified` kept. | `setEtag="false"` drops the `ETag` alone. |

## Conditional requests, scope, and errors

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| CC11 | `If-Modified-Since` equal to `Last-Modified` under `UseMaxAge` and under `DisableCache`. | `304 Not Modified` carrying `Cache-Control: max-age=86400` and `Cache-Control: no-cache` respectively, with the `ETag`. | The header rides the 304 too. A far-future `If-Modified-Since` answered 200. |
| CC12 | No root section; `<location path="sub">` with one hour; `folder/web.config` with two hours. | Root file: no header. `/sub/static.txt`: `max-age=3600`. `/folder/static.txt`: `max-age=7200`. | The section scopes per path, both ways. |
| CC13 | `UseMaxAge` one day plus a `caching` profile on `.txt` with `location="Any"`; a `.txt` and a `.json`. | `.txt`: `Cache-Control: public,max-age=86400`. `.json`: `max-age=86400`. | The profile's word goes first, the `max-age` after, comma-joined, one header. |
| CC14 | `cacheControlMaxAge="abc"`. | Static file: 500.19. Page: 200. | A bad value fails the static requests alone, consumption-scoped as usual. |
| CC15 | `UseMaxAge` with `*.txt` mapped to ASP.NET's managed `System.Web.StaticFileHandler`. | `Cache-Control: public`, `Expires` one day ahead, no `max-age`. | The managed handler's own fixed headers, not the setting: `clientCache` lives in the native module and never reaches a managed handler. |

## Conclusions for the port

- The port's `StaticFileBridgeHandler` already stands in for the native
  module and already writes the caching profile's word or suppresses the
  default `Cache-Control`; the managed handler's own `public` and one-day
  `Expires` are fenced off. `clientCache` is one more input to that same
  header, in the same place, on the 200, the 206 and the 304 alike (CC1, CC2,
  CC11).
- The wire shapes are small: `max-age=<seconds>`, an optional custom text in
  front, the profile word in front of that, all comma-joined without spaces;
  `Expires` verbatim under `UseExpires`; `no-cache` under `DisableCache`; no
  `ETag` when `setEtag` is false (CC4-CC10, CC13).
- Per-path scoping exists (CC12); root-only with a refusal for a folder or
  `<location>` section is the first cut the other static sections took.
- A bad span or mode fails activation naming the file and attribute, where
  IIS failed the static requests alone (CC14).
