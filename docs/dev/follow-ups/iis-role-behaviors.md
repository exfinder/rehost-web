# IIS-role behaviors

Kestrel replaces IIS behavior outside System.Web. Current support lives in
[compatibility](../compatibility.md); configuration scope belongs to
[IIS configuration tenants](iis-integration-plan.md).

## Open contract

- Filtering switches beyond delivered extension, segment, URL/query, verb and
  declared-content-length limits: double escaping and high-bit characters.
- Wire canonicalization of malformed static names, raw non-ASCII or `#`, and
  trailing separators: both hosts refuse these shapes but status/substatus differs.
- Compression ownership and `TransmitFile`/static-file bypass when a host adds
  ASP.NET Core response compression.
- Explicit Windows/anonymous authentication, W3C logging, kernel/output caching,
  ISAPI filters, directory browsing and warm-up boundaries.

## Done when

Every reached IIS request-path behavior has an explicit support boundary or
exclusion, with executable tests for supported behavior.
