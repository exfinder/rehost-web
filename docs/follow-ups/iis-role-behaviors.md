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

- ~~Default documents~~ — restored (ledger P67): `/` serving `Default.aspx`,
  full directory-request surface including the courtesy 301 and 403-class
  refusal.
- ~~Content types~~ — restored (ledger P60). In integrated mode System.Web swapped its hardcoded
  344-entry table for IIS's `<staticContent>` map
  (`MimeMappingDictionaryIntegrated` — the seam already exists in the
  imported code). The port runs the classic table, which predates `.svg`,
  `.json`, and `.woff/.woff2`: they pass the P58 gate but serve as
  `application/octet-stream`, and an SVG will not render. The IIS reading
  behind the gate carries the types as well as the extensions.
- ~~Directory requests~~ — restored with default documents (ledger P67):
  default-doc-or-403 with directory browsing off.
- Request filtering beyond hidden segments: double-escaping rejection, URL
  and query-string length limits, verb rules, high-bit characters,
  `maxAllowedContentLength` versus `maxRequestLength` interplay. Read
  (ledger P72, [readings](../research/iis-url-canonicalization-readings.md)):
  `fileExtensions` denial answers 404.7 where the golden forbidden handler
  answers 403 (`/web.config`, `/x.config`); double escaping (`%252e`) and
  invalid Windows name characters (`%25 %3C %3E * %3F`, bad UTF-8) in a
  static URL are 404 from the native static tier where the port's managed
  `ValidatePath` answers 400; a static file with a trailing separator is
  IIS 500.0 (`ERROR_DIRECTORY`) versus 404; a raw `#` or non-ASCII byte is
  http.sys 400 versus Kestrel/ASP.NET 404/400. Nothing is served on either
  side of any of these.
- ~~URL normalization IIS performed before ASP.NET looked: `.` and `..`
  collapsing, `%`-decoding order, `\\` and repeated separators, above-root
  refusal, path-info split~~ — restored (ledger P72). Trailing dots/spaces
  stay: 404 on both for static and page URLs; an existence-agnostic handler
  URL with a trailing space runs on the port and is unmappable on IIS.
- Error shapes IIS owned: `httpErrors` versus `customErrors` boundaries for
  400/404/413-class responses that never reached ASP.NET.
- Compression (`StaticCompressionModule`/`DynamicCompressionModule`,
  `<urlCompression>`/`<httpCompression>`): today only if the host adds
  ASP.NET Core's response-compression middleware, which the adapter's sendfile
  path bypasses for `TransmitFile`/static files; decide restore-or-exclude here.
- Explicit exclusions to record: Windows/anonymous auth beyond current
  support, W3C logging, kernel/output caching at the IIS tier,
  ISAPI filters, application warm-up.

## Done when

Every entry in IIS's default request path is classified with evidence, the
missing-worth-restoring set is either delivered or split into stories, and
the exclusions are named in the compatibility map rather than implied.
