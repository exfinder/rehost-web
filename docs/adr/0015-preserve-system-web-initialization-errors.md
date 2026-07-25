---
status: accepted
---

# Preserve System.Web initialization error responses

When System.Web has initialized enough request machinery to format a cached
initialization failure, the triggering request receives its normal
`HttpResponse.ReportRuntimeError` response. The host adapter must not replace
that response with a generic ASP.NET Core error.

Only failures before `HttpContext` and `HttpResponse` are usable fall back to
host-side error handling. Application-fatal policy is applied after preserving
the triggering response where safely possible.
