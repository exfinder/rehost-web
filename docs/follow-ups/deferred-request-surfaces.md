# Deferred request surfaces

Status: open. Priority: medium. Depends on first runnable request.

The first milestone intentionally excludes:

- POST bodies, forms, multipart uploads, and client certificates;
- cookies, session, authentication, authorization, and impersonation;
- redirects, `Response.End`, transfer/execute, and timeouts;
- async pages/handlers and disconnect cancellation;
- `Global.asax`, custom modules, resources, master pages, and application
  restart;
- streaming, large/file responses, compression, and protocol upgrades.

After the GET fixture works, split this list into capability stories ordered by
application demand and semantic risk. Each resulting story must update the
compatibility map. Exclusion here does not authorize silent fallback.

Done when every item is either an owned story with acceptance criteria or an
explicit product-scope rejection.
