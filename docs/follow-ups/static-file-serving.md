# Static file serving

Status: done 2026-08-07 (ledger P54 hotfix, P58 story). The review questions
the P54 hotfix left open are each resolved below.

## Delivered (ledger P58)

- **Native transfer.** The response spool carries file ranges by reference
  (ordered beside buffered byte runs) and the commit hands them to Kestrel's
  `SendFileAsync` — the same sendfile path the platform's static middleware
  uses. No more copy-through-buffer; every range is re-validated before the
  first byte leaves, so a file that shrank after spooling fails while a status
  can still be produced.
- **Serving authority stays System.Web**, matching IIS integrated mode with
  managed modules engaged: authorization and `<location>` rules protect
  statics, hidden segments and the P57 case folding apply automatically.
  `UseStaticFiles`-in-front was considered and rejected: it silently bypasses
  `<location>` authorization, and the forbidden-segment and case rules would
  need rebuilding in middleware.
- **Revalidation 304s** (port-owned addition in `StaticFileHandler`): the
  managed handler never carried `If-Modified-Since`/`If-None-Match` because
  IIS's native module owned them on every real deployment. Wire readings on
  real IIS 10 (the wire rig) confirm the shape: 304, empty body, `ETag`
  re-sent; this port also re-sends `Last-Modified`, which IIS omits —
  RFC-permitted, recorded as the one shape difference. `If-None-Match` wins
  over `If-Modified-Since`, as on IIS.
- **The extension gate** (port-owned, `IisStaticContent` +
  `DefaultHttpHandler`'s static fallback): IIS refused extensions outside its
  static content-type list, so a stray `*.bak` beside `web.config` never
  downloaded. The list is read from a default IIS 10.0.26100
  `applicationHost.config` (424 mimeMap entries, 422 distinct extensions) —
  IIS's contribution, deliberately absent from the Framework golden configs.
  Directories pass through untouched; an application serves an off-list
  extension by mapping it to `StaticFileHandler` in its own `web.config`,
  which routes around the gate explicitly.
- **The conditional matrix is pinned** by `StaticFilesOverKestrelTests`:
  304 both ways, range 206 with the exact slice, 416 naming the length, HEAD
  with headers and no body, the off-list 404, and an unmapped known extension
  serving with its content type.

## Remaining boundaries

- Application code calling `Response.TransmitFile`/`WriteFile` with a
  Unix-rooted path still hits Framework's physical-vs-virtual classification;
  on the [decision-6 checklist](portable-filesystem-and-config-path-semantics.md),
  fixed on first real contact.
- Content types come from `MimeMapping`'s Framework table, which predates a
  few modern types (`.json`, `.woff2` serve, but as the generic binary type
  where the table lacks an entry). Recorded, not yet measured against a real
  consumer.
- `If-Range` behavior is imported and untested beyond the range tests above.
