# Header amendment after Response.End

Status: done 2026-08-06 — reopened (ledger P55, `HeaderAmendmentOverKestrelTests`).
`End`'s internal flush defers header generation to the final flush, so headers
and cookies stamped in `EndRequest` ship as Framework's abort arm delivered
(readings R19–R24); an application's own `Flush()` still seals. Accepted
residue: the ended response leaves chunked rather than with Framework's exact
`Content-Length`. Story:
[Response.End and request termination](response-end-and-termination-plan.md).

## The gap

`Response.End` — and the terminating `Redirect` — flush synchronously at the
call, Framework's non-abort arm, because the abort arm needed `Thread.Abort`
(ledger P52). After the flush, `_headersWritten` is set and any later header
append, for example in `EndRequest`, throws Framework's *"headers have been
sent"*. On Framework's abort arm the headers stayed open until the final
flush, so a module stamping headers in `EndRequest` — auth, telemetry,
correlation ids, not exotic — silently loses them on every ended or redirected
request here. Accepted deviation, recorded in the compatibility map's
`Response.End` row; the sample application's Termination page demonstrates it
(the `X-Sample-EndRequest` header missing from `mode=end`).

## Measured (readings R19–R24, 2026-08-06)

The abort-arm behavior is measured, not inferred: on 4.8.1, a header appended
in `EndRequest` after `Response.End` **reaches the transport** (R20), on the
terminating redirect's 302 (R21), and matching `CompleteRequest` (R22); a
cookie added in `EndRequest` after `End` ships too (R23). After a real
`Flush()`, appending **throws** *"headers have been sent"* and the header is
genuinely lost (R24) — so an application's own `Flush()` must keep sealing
whatever this story decides. Full rows in the
[termination plan's readings](response-end-and-termination-plan.md#framework-readings).

## Why it is revisitable at all

Nothing reaches the wire at `End`: the adapter's `FlushResponse` is a no-op and
the whole response commits once from the `ResponseSpool` after `EndOfRequest`.
The seal is `HttpResponse._headersWritten` — imported-source state faithfully
tracking a transport event that, on this host, has not actually happened. A
port seam could keep headers amendable between `End` and the commit without any
byte having left the process. The body-side semantics (bytes after `End`
discarded) are unaffected either way.

## Decision (2026-08-06)

Reopened, on the strength of R19–R24: Framework demonstrably shipped late
headers and late cookies after `End` and the terminating `Redirect`, so keeping
the seal meant losing measured behavior — session and auth cookies stamped in
`EndRequest` being the concrete casualty. The seam is `End`-specific
(`_endHeadersDeferred` in `HttpResponse`): body bytes still leave for the spool
at the call, header generation waits for the final flush, and an application's
own `Flush()` never latches, preserving R24's seal. Pinned for all four shapes
(`End`, terminating `Redirect`, `CompleteRequest`, post-`Flush`) by
`HeaderAmendmentOverKestrelTests`.
