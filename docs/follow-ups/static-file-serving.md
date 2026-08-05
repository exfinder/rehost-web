# Static file serving

Status: open — review the P54 hotfix. Priority: medium.

## What landed (ledger P54)

The first reached static-file path, applied as a narrow hotfix while building
the sample application, and worth a deliberate review pass:

- `HttpResponse.TransmitFile` split at `GetNormalizedFilename` into an internal
  `TransmitFileTranslated`; `StaticFileHandler` enters past the classification
  with its already-translated path.
- `AspNetCoreWorkerRequest.SendResponseFromFile(string, ...)` no longer refuses;
  it spools the file range through `ResponseSpool.WriteFile`.
- `page` fixture maps `*.css` to `StaticFileHandler`;
  `PageOverKestrelTests` covers the 200 + bytes + `text/css` and the 404 miss.

## Review questions

- **Seam placement.** Is call-site bypass (`TransmitFileTranslated`) the right
  durable seam, or should physical-vs-virtual classification own a portable
  answer once, where every `GetNormalizedFilename` caller (both `WriteFile`
  overloads included) gets it? Application code calling
  `Response.TransmitFile`/`WriteFile` with a Unix-rooted path still
  misclassifies and doubles the root — decide whether that surface gets the
  same treatment, an explicit refusal, or a documented gap.
- **Spool memory.** `ResponseSpool.WriteFile` copies the file range into the
  `FileBufferingWriteStream` (memory under 32 KiB, then a temp file). Framework
  under IIS handed the filename to the server (`TransmitFile`) and never
  buffered. A large download is copied to disk before the first byte leaves —
  decide whether the commit path should stream file elements directly.
- **Unassessed handler arms.** Range requests, `If-Range`,
  `If-Modified-Since`/304, ETag revalidation, and HEAD run through untested
  `StaticFileHandler` code over this adapter.
- **`DefaultHttpHandler`.** Unmapped extensions still fall through to it —
  its behavior here (Framework deferred to IIS) is unassessed, so an
  application serving `.js`/images needs every extension mapped explicitly.
  Decide whether the shipped configuration should map common content
  extensions, which Framework left to IIS's static handling.

## Evidence

`PageOverKestrelTests.Serves_A_Static_File_Through_StaticFileHandler` fails
against the pre-fix runtime with the doubled-root 500; the spool ordering and
short-file refusal are covered by `AspNetCoreWorkerRequestTests`.
