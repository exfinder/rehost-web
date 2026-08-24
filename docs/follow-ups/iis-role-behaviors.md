# IIS-role behaviors

Kestrel replaces IIS behavior that lived outside System.Web. Delivered pieces
are recorded in the portability ledger; this file tracks only remaining IIS
request-tier gaps.

## Open audit

- Request filtering beyond hidden segments and file extensions: double
  escaping, URL/query limits, verb rules, high-bit characters, and
  `maxAllowedContentLength` interaction with `maxRequestLength`.
- Wire-only canonicalization differences: malformed static names, raw
  non-ASCII or `#`, and static trailing separators. Both hosts refuse these,
  but status/substatus differs; readings are in
  [URL canonicalization](../research/iis-url-canonicalization-readings.md).
- `httpErrors` versus `customErrors` ownership for failures before System.Web.
- Compression ownership and the `TransmitFile`/static-file bypass when the
  host adds ASP.NET Core response compression.
- Explicitly classify Windows/anonymous authentication beyond current support,
  W3C logging, IIS kernel/output caching, ISAPI filters, directory browsing,
  and application warm-up.

## Done when

Every default IIS request-path contribution is either supported with evidence
or named as an exclusion in the compatibility map.
