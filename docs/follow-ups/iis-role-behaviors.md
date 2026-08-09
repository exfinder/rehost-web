# IIS-role behaviors

## Problem

On Framework, the observable contract was produced by two parties: System.Web
and IIS. This port replaces IIS, and behaviors IIS owned do not fail loudly —
they silently vanish. Three have been found incidentally, each by accident
rather than by plan:

- P54: the static transmit path (found building the sample application);
- P58: the static content-type gate and revalidation 304s (found by a review
  question — the port served `*.bak` and never answered 304);
- P59: hidden segments (found by a security question — `App_Data` served its
  files; System.Web's own retired `s_forbiddenDirs` fossil proves the job was
  deliberately handed to IIS).

Three instances of one root cause justify enumerating the class.

## The audit

Probe-driven, like the 2026-08-06 filesystem audit: enumerate IIS's
request-processing contributions from a default `applicationHost.config` and
the IIS pipeline module list (both readable on `win-oracle`, which carries a
real IIS 10 install and the wire rig), then measure the port's behavior for
each and classify:

- **restored** — P54, P58, P59 so far;
- **covered** — by Kestrel, the golden configs, or existing port behavior
  (verify, don't assume: the extension deny list is covered by the golden
  forbidden mappings, but that was only confirmed by probing);
- **missing, worth restoring** — becomes a story with wire readings and a
  ledger row;
- **out of contract** — recorded exclusions with a sentence of rationale.

## Candidate list (unverified — the audit's starting point, not its bound)

- Default documents: `/` serving `Default.aspx`. The standout
  consumer-visible suspect; every legacy application's root URL relies on it.
- ~~Content types~~ — restored (ledger P60). In integrated mode System.Web swapped its hardcoded
  344-entry table for IIS's `<staticContent>` map
  (`MimeMappingDictionaryIntegrated` — the seam already exists in the
  imported code). The port runs the classic table, which predates `.svg`,
  `.json`, and `.woff/.woff2`: they pass the P58 gate but serve as
  `application/octet-stream`, and an SVG will not render. The IIS reading
  behind the gate carries the types as well as the extensions.
- Directory requests: default-doc-or-403 with directory browsing off.
- Request filtering beyond hidden segments: double-escaping rejection, URL
  and query-string length limits, verb rules, high-bit characters,
  `maxAllowedContentLength` versus `maxRequestLength` interplay.
- URL normalization IIS performed before ASP.NET looked: trailing
  dots/spaces, `.` and `..` collapsing, `%`-decoding order.
- Error shapes IIS owned: `httpErrors` versus `customErrors` boundaries for
  400/404/413-class responses that never reached ASP.NET.
- Explicit exclusions to record: Windows/anonymous auth beyond current
  support, compression, W3C logging, kernel/output caching at the IIS tier,
  ISAPI filters, application warm-up.

## Done when

Every entry in IIS's default request path is classified with evidence, the
missing-worth-restoring set is either delivered or split into stories, and
the exclusions are named in the compatibility map rather than implied.
