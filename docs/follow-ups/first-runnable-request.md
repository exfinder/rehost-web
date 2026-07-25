# First runnable request

Status: open. Priority: high. Depends on the phase-one warning-free runtime
build.

## Goal

Serve `GET /Default.aspx` through Kestrel, `HttpWorkerRequest`, `HttpRuntime`,
dynamic page parsing/compilation/loading, code-behind execution, and response
output on every supported platform.

The fixture contains one `.aspx`, code-behind, and `web.config`. It may use a
query string and deterministic request headers. It excludes `Global.asax`,
resources, master pages, session, authentication, custom modules, POST,
redirects, and async pages.

## Contract

- Cross-platform or explicitly unsupported everywhere; never Windows-only
  partial behavior.
- Preserve System.Web semantics where evidence exists.
- POC history is a hazard map, never implementation authority.
- Each technical decision and deviation is recorded in a current compatibility
  document or ADR.
- Intermediate work has focused tests where feasible. If pipeline completion
  blocks testing, record the invariant, blocker, exact deferred test, and link
  to the integration story.

## Order

1. Host adapter and completion contract.
2. Bootstrap, configuration, paths, and filesystem behavior.
3. Startup portability and minimal feature profile.
4. Response output, compilation/loading, compiler policy, and ViewState keys.
5. Process policy and portable diagnostics.
6. Dynamic ASPX integration and compatibility-map update.

## Done when

A Linux integration test starts a real Kestrel host, requests the fixture, and
asserts status, selected headers, rendered body, code-behind output, and clean
request completion. macOS and Windows must use the same runtime contract.
