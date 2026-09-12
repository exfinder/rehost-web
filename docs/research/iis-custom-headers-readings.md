# IIS custom response headers readings

Evidence for the `httpProtocol/customHeaders` tenant in the
[IIS configuration plan](../follow-ups/iis-integration-plan.md). CH1-CH24 were
observed on full IIS 10 on `winbox`, 2026-09-13.

## Method

One Integrated v4.0 pool, one site on port 8112, one application per case
(`hc` control, `h1`-`h10`). `probe.aspx` answered `text/plain` and, on request,
called `Response.AppendHeader`, `Response.Headers.Remove`,
`Response.ClearHeaders`, `Response.Redirect`, set a status, or threw. Each
application also held `static.txt`, a `sub/` folder, and for `h4` a `folder/`
with its own `web.config`. Requests used on-box `curl`; `httpErrors` default.
The server-level baseline is IIS's own, whose `customHeaders` carries
`X-Powered-By: ASP.NET`.

## Where the headers land

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| CH1 | Control application, no section. | `X-Powered-By: ASP.NET` on the page, the static file and the native 404. | The baseline entry is an ordinary custom header inherited by every application. |
| CH2 | `add` `X-Custom`, `X-Frame-Options`, `Cache-Control: no-store`; request a page, a static file, a native 404, a managed 404, a `Response.Redirect`, an unhandled exception, a page-set 500. | Every response carries the added headers, after `X-Powered-By`, in document order. | The headers ride every response the server sends, whatever produced it. |
| CH3 | The same on HEAD, OPTIONS, a 304 (`If-None-Match: *`), the directory 403.14, the request-filtering 404.8, and IIS's own `301` slash redirect. | All carry them. The 404.8 answer adds `Connection: close` as before. | No status or verb is exempt; native early answers included. |
| CH4 | Headers on the wire, order. | `Cache-Control`, `Content-Type`, `Server`, `X-AspNet-Version`, `X-Powered-By`, then the application's adds in document order, then `Content-Length`. | Custom headers append after the response's own headers. |

## Interaction with the application's headers

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| CH5 | Page appends `X-Custom: app` while the section adds `X-Custom: one`; same with `X-Frame-Options: SAMEORIGIN` against `DENY`. | Two headers on the wire: the page's first, the section's after `X-Powered-By`. | A same-name custom header duplicates; neither replaces the other. |
| CH6 | Section adds `Cache-Control: no-store` on a page whose default is `private`; page appends `Cache-Control: private` explicitly; a static file. | One header, comma-joined: `Cache-Control: private,no-store` on the page, `no-store` alone on the static file. | `Cache-Control` coalesces into the existing header instead of duplicating. |
| CH7 | Section adds `Content-Type: text/x-bogus` and `Server: Mine`. | `Content-Type: text/plain; charset=utf-8,text/x-bogus` on the page, `text/plain,text/x-bogus` on the static file; `Server` appears twice, `Microsoft-IIS/10.0` then `Mine`. | `Content-Type` coalesces like `Cache-Control`; `Server` duplicates like any other name. |
| CH8 | Page calls `Response.Headers.Remove("X-Custom")`; page calls `Response.ClearHeaders()`. | The section's `X-Custom` is on the wire in both cases. | The application cannot remove or clear a configured header; the module adds it after managed code has finished. |
| CH9 | Section `<remove name="X-App" />` while the page appends `X-App: fromapp`. | `X-App: fromapp` is sent. | `remove` edits the configured collection only; it never strips an application header. |
| CH10 | Section `<remove name="X-Powered-By" />`; page then appends `X-Powered-By: fromapp`. | The baseline header is gone from the page, the static file and the native 404; the page's own value is sent. | Removing the inherited entry is how an application drops `X-Powered-By`, and it leaves the name free for the application. |

## Configuration shape

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| CH11 | `<clear />` then one `add`, at application level. | Accepted; `X-Powered-By` is gone and the add is sent. | The collection is not locked. |
| CH12 | Two `add` rows with the same name. | Every request 500.19, `0x8007000d`, "Cannot add duplicate collection entry of type 'add' with unique key attribute 'name'", config source lines named. | Names are unique keys. |
| CH13 | `add` of `X-Powered-By` at application level, no `remove` first. | The same 500.19 for every request. | Adding an inherited name without removing it is a duplicate. |
| CH14 | `add name="x-custom"` and `add name="X-Custom"`. | The same 500.19. | The key is case-insensitive. |
| CH15 | `add name="X-Empty" value=""`. | Accepted; no `X-Empty` header on the wire. | An empty value sends nothing. |
| CH16 | `add name="X-Multi" value="a, b"`. | `X-Multi: a, b` verbatim. | Values pass through untouched. |
| CH17 | `<location path="sub">` adding `X-Loc`. | `X-Loc` on `/sub/probe.aspx`, `/sub/static.txt`, and on IIS's own `301` for `/sub`; absent at the root. The root's `X-Root` is present under `sub` too. | `<location>` scopes by request path and inherits the root's rows, unlike the rewrite section (UR23). |
| CH18 | `folder/web.config` adding `X-Folder` and removing `X-Root`. | Under `folder/`: `X-Folder` on the page, the static file and the native 404; `X-Root` gone; `X-Powered-By` kept. At the root: `X-Root` present, `X-Folder` absent. | Folder files add to and remove from the inherited collection for their subtree. |

## Round 2: removal switches, timing, encoding, upgrades

| ID | Stimulus | Observed result | Contract evidence |
|---|---|---|---|
| CH19 | `<remove name="Server" />` in `customHeaders`. | Accepted; `Server: Microsoft-IIS/10.0` still sent on every response. | The collection cannot touch a header the server itself writes; the hardening line is inert. |
| CH20 | `<security><requestFiltering removeServerHeader="true" /></security>`. | `Server` gone from the page, the static file and the native 404; `X-Powered-By` and the custom adds stay. | That attribute is the real switch for `Server`; it is independent of `customHeaders`. |
| CH21 | A page that writes, flushes, sleeps, then writes more. | `Transfer-Encoding: chunked`; the custom headers are on the head that left with the first flush. | The headers are applied when the head is sent, not when the request ends. |
| CH22 | `value="café"` in a UTF-8 `web.config`. | One byte on the wire, `0xE9`: `X-NonAscii: caf\xE9`. | Header values leave as ISO-8859-1, whatever the file encoding. |
| CH23 | `add name="X Bad"` with a space in the name. | Accepted and sent raw: `X Bad: x`. | Names are not validated; a client that rejects the line is the only guard. |
| CH24 | A WebSocket upgrade to an `.ashx` that accepts it, `X-Custom: ws` configured. | The `101 Switching Protocols` head carries `X-Custom: ws` and `X-Powered-By: ASP.NET` beside `Upgrade`, `Connection` and `Sec-WebSocket-Accept`. | The upgrade response is a response like any other. |

## What Kestrel does with the same shapes

Measured on macOS with a bare Kestrel 10 app, headers appended in the handler
and in a `Response.OnStarting` callback, one early flush:

- Two values under one name are written as two lines, for `X-Dup` and for
  `Cache-Control` alike. IIS coalesced `Cache-Control` and `Content-Type`
  (CH6, CH7), so the port must join those two by hand and leave the rest.
- Headers appended in `OnStarting` are on the head that leaves with the
  first flush, matching CH21.
- A non-ASCII value fails the response with a 500 unless
  `ResponseHeaderEncodingSelector` allows it; with Latin-1 selected the byte
  is `0xE9`, matching CH22. The port already installs a selector for response
  headers, so the value takes that path.

## Conclusions for the port

- One mechanism, one place: a callback that runs when the response head is
  written, on every response the host produces, including its own refusals,
  the rewrite step's early answers, and 304s (CH2, CH3).
- Order is inherited rows first, then the application's adds in document
  order, appended after the response's own headers (CH4). The inherited row
  is `X-Powered-By: ASP.NET` from the shipped baseline (CH1), which the port
  does not send today.
- A same-name header duplicates, except `Cache-Control` and `Content-Type`,
  which coalesce with a comma (CH5-CH7). The application can neither remove
  nor clear a configured header (CH8); `remove` only edits the collection
  (CH9, CH10).
- Configuration refusals to reproduce at activation: duplicate names,
  case-insensitively, including a re-add of an inherited name (CH12-CH14).
  `<clear/>` and empty values are accepted (CH11, CH15).
- Per-path scoping exists on IIS (CH17, CH18). Root-only is the smaller
  first cut; a folder or `<location>` section must then fail activation
  rather than fall silent, as the rewrite section does.
- `remove name="Server"` is inert and `removeServerHeader` is the switch
  (CH19, CH20); the port's own `Server` header should follow the attribute
  and ignore the row. Values go out as ISO-8859-1 bytes (CH22), names are
  not validated (CH23), and the 101 upgrade carries the headers too (CH24).
