# Header amendment after Response.End

Status: open. Priority: low. Depends on
[Response.End and request termination](response-end-and-termination-plan.md)
(done) and [response buffer ownership](response-buffer-ownership.md).

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

## Why it is revisitable at all

Nothing reaches the wire at `End`: the adapter's `FlushResponse` is a no-op and
the whole response commits once from the `ResponseSpool` after `EndOfRequest`.
The seal is `HttpResponse._headersWritten` — imported-source state faithfully
tracking a transport event that, on this host, has not actually happened. A
port seam could keep headers amendable between `End` and the commit without any
byte having left the process. The body-side semantics (bytes after `End`
discarded) are unaffected either way.

## Decision needed

- Keep the deviation (headers close at `End`, matching the non-abort arm
  Framework also had) — cheapest, already documented; or
- Reopen header amendment until commit, matching the abort arm applications
  actually ran under IIS integrated mode.

Either way the outcome belongs in the compatibility map row, and a scenario
over Kestrel asserting the chosen behavior for `End`, terminating `Redirect`,
and `CompleteRequest` (whose headers stay open today) should pin it.
