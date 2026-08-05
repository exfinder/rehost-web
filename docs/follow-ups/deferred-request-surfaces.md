# Deferred request surfaces

Status: current. Priority: high. Slice 4; depends on completed slices 0–3.

The completed slices still exclude or only partially cover:

- client certificates;
- cookies, session, authentication, authorization, and impersonation;
- redirects, `Response.End`, transfer/execute, and timeouts;
- async pages and cancellation/termination beyond the verified handler cases;
- user controls, master pages, `.ashx`, resource handlers, and application
  restart;
- streaming, large/file responses, compression, and protocol upgrades.

Split this list into capability stories ordered by application demand and
semantic risk. Each resulting story must update the compatibility map.
Exclusion here does not authorize silent fallback.

Existing stories:
[cookies](cookies.md);
[entity-body bridge](request-entity-body-bridge.md) and
[postback and form parsing](postback-and-form-parsing.md), which together own
POST bodies, forms, view state, control state, and multipart reads and
saves;
[Response.End and request termination](response-end-and-termination-plan.md),
which owns the termination half of
[request termination/timeouts](request-termination-and-timeouts.md),
[route escaping](route-url-escaping.md), and
[WebResource timestamps](web-resource-assembly-timestamps.md). Portable treatment
of IIS-hosted application settings belongs to
[`system.webServer` configuration compatibility](system-webserver-configuration-compatibility.md).

Done when every item is either an owned story with acceptance criteria or an
explicit product-scope rejection.
