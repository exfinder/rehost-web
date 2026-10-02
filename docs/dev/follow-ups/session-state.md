# Session state

`InProc` and `Custom` are delivered through the shipped `Session` module.
Standing tests cover round-trip, exclusive versus read-only acquisition,
abandonment, custom-provider calls, and refusal of unsupported modes. Current
support lives in the [compatibility map](../compatibility.md); SQL and state
server modes have separate follow-ups.

The cache uses 20-second expiry buckets and configuration enforces a one-minute
session-timeout floor. Expiry tests must account for both constraints.

## Open

- Prove `Session_End` on timer expiry, not only abandonment.
- Assess cookieless identity separately: `UseUri` is managed URL rewriting;
  `AutoDetect` and `UseDeviceProfile` additionally depend on browser
  capabilities.
- Implement [SQL mode](session-sql.md) and
  [state server mode](session-state-server.md).

`useHostingIdentity` is currently degenerate because impersonation is inert;
both values use the process identity.

## Done when

Expiry has three-platform evidence; each cookieless mode has an explicit
support boundary; out-of-process modes meet their own follow-up contracts.
