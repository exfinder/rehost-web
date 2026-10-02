# Forms authentication, roles, profile, anonymous identity

The shipped `<modules>` baseline registers Forms Authentication, roles,
profiles, anonymous identity, and output cache. Fake-provider journeys cover
redirect, sign-in, role-cookie reuse, anonymous/profile persistence, and output
cache; Windows mode fails activation. Current support lives in the
[compatibility map](../compatibility.md).

## Open

- Add an `apps/` sample that registers, signs in, checks a role, and signs out
  against real `aspnetdb` on all supported platforms.
- Decide whether swallowed ticket-restoration failures need a diagnostic that
  does not report ordinary tampering.

## Done when

The SQL-provider journey has a Framework baseline and three-platform evidence;
ticket diagnostics, if added, distinguish configuration/platform failures from
invalid client cookies.
