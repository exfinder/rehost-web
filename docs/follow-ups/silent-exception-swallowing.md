# Silent exception swallowing in imported Reference Source

Status: open. Priority: medium. Depends on nothing; blocked on a noise policy.

## Problem

Imported Reference Source contains 204 `catch` blocks with empty bodies across
89 files, concentrated in `HttpRuntime` (16), `Security/Roles` (10),
`HttpRequest`, `UI/Page`, `Util/FileUtil`, and `Compilation/BuildResultCache`
(6 each). A discarded exception during a port is not a minor inconvenience: the
pipeline slice presented as an empty `200` with no status, no headers, and no
flush, and the cause — two native dependencies and an unresolvable `bin`
assembly — was invisible until one of those blocks was instrumented (P31).

## Current rule

No silent catch survives contact with a slice. A slice instruments the swallows
it actually traps against, and records them in the portability ledger. Blocks
that a slice never reaches stay untouched, because changing unreached imported
code obliges a test that fails without the change.

## Required decisions before any sweep

- Which swallows are deliberate and must stay quiet. `FileUtil` probe-and-ignore,
  `ErrorFormatter` fallbacks, and shutdown paths are load-bearing; a channel that
  fires on every request teaches readers to ignore it.
- Severity per site, so expected-and-ignorable is distinguishable from
  lost-and-fatal without reading the call site.
- Whether reporting belongs at the catch site or behind a helper, given that
  every instrumented site is a permanent diff against upstream that a future
  reader must diff past.
- How the channel behaves with no listener attached, on hot paths.

## Verification

A slice that traps a swallowed exception can name it from diagnostic output
alone, without a debugger and without a bisect.

## Done when

Every reached swallow reports, the expected-and-quiet set is enumerated with
reasons, and the ledger records the deviation class once rather than per site.
