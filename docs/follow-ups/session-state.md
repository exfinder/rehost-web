# Session state

`InProc` and `Custom` are delivered through the shipped `Session` module.
Standing tests cover round-trip, exclusive versus read-only acquisition,
abandonment, custom-provider calls, and refusal of unsupported modes. Current
support lives in the [compatibility map](../compatibility.md); SQL and state
server modes have separate follow-ups.

## Framework readings

Captured 2026-08-15 on IIS Express 10 / Framework 4.8.1 with default
configuration:

| IDs | Result |
| --- | --- |
| S1-S7 | Defaults: `InProc`, `ASP.NET_SessionId`, `UseCookies`, 20 minutes, `regenerateExpiredSessionId=true`, `useHostingIdentity=true`, `SameSite=Lax` |
| S8-S9 | New stored session cookie: 24 lower-case alphanumerics, `path=/; HttpOnly; SameSite=Lax`; a valid returning cookie is not reissued |
| S10-S14 | Without `Session_Start`, no cookie is issued for untouched/read-only new state; reading `SessionID` creates a request-local ID; a write stores and issues it |
| S15 | Declaring `Session_Start` stores every session-enabled request and therefore issues a cookie |
| S16-S17 | Returning cookie preserves ID/value and reports `InProc`, writable, 20-minute state |
| S18 | A valid unknown client ID is adopted as a new empty session without reissuing the cookie |
| S19-S21 | Disabled pages/plain handlers have no session; `IRequiresSessionState` gets writable persistent state |
| S22-S24 | Read-only pages/handlers can mutate the live InProc collection; the mutation persists, unlike an out-of-process store |
| S25-S27 | Same-session writes serialize; different sessions and same-session read-only requests overlap |
| S28-S30 | `Abandon` keeps ID/state for the current request; next request reuses the ID as new empty state; `Session_End` runs with no current context |

The shipped cache uses 20-second expiry buckets and configuration enforces a
one-minute timeout floor. An expiry callback test therefore costs roughly
60-80 seconds per platform.

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
