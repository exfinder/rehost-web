# Cookies

Status: in progress. Slice 4, split out of
[deferred request surfaces](deferred-request-surfaces.md).

## Scope

In:

- request `Cookie` header ingestion through `AspNetCoreWorkerRequest` into
  `HttpRequest.Cookies`, including sub-key values and the `$Path`/`$Domain`
  attribute forms;
- response `Set-Cookie` emission through the response spool and the ASP.NET Core
  commit: several cookies, attribute rendering (`domain`, `expires`, `path`,
  `secure`, `HttpOnly`, `SameSite`), and the classic-pipeline meaning of
  `Response.Cookies` mutation;
- `HttpCookie` semantics an application observes: sub-key collections,
  the defaults a cookie is born with, and `Shareable`;
- the `<httpCookies>` configuration defaults and the `appSettings` switches that
  change cookie behavior;
- request validation of cookie values.

Out:

- session state, forms authentication, anonymous identification, and roles —
  every cookie those subsystems own belongs to their own story;
- cookieless modes (`CookielessHelper`, `AppPathModifier`), which are reached
  from those same subsystems;
- output caching interaction (`Shareable`, `SuppressCachingCookiesIfNecessary`),
  which needs the caching slice;
- `Set-Cookie` written through `Response.Headers`/`AddHeader` rather than the
  cookie collection, which is the header story, not the cookie story;
- the integrated-pipeline incremental cookie path
  (`GenerateResponseHeadersForCookies`, `aspnet:AvoidDuplicatedSetCookie`,
  `HttpCookie.IsInResponseHeader`), unreachable here: this port is the classic
  pipeline ([ADR 0001](../adr/0001-classic-managed-pipeline.md)), where headers
  are generated once in `WriteHeaders`.
