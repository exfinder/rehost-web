# Forms authentication, roles, profile, anonymous identity

The shipped `<modules>` baseline registers Forms Authentication, roles,
profiles, anonymous identity, and output cache. Fake-provider journeys cover
redirect, sign-in, role-cookie reuse, anonymous/profile persistence, and output
cache; Windows mode fails activation. Current support lives in the
[compatibility map](../compatibility.md).

## Framework readings

Captured 2026-08-22 on IIS Express 10 / Framework 4.8.1 with in-memory
providers and a literal machine key:

| ID | Result |
| --- | --- |
| R-FA1 | Anonymous denial redirects to `/Login.aspx?ReturnUrl=%2fSecret%2fDefault.aspx` with Framework's moved page |
| R-FA2 | Redirect issues `.ASPXANONYMOUS`, 70-day expiry, `HttpOnly`, no `SameSite` |
| R-FA3 | Anonymous request carries `GenericIdentity`, `RolePrincipal`, and anonymous profile GUID |
| R-FA4 | `SetAuthCookie` issues session `.ASPXAUTH`, `HttpOnly; SameSite=Lax` |
| R-FA5 | Next request is authenticated with a named `FormsIdentity` |
| R-FA6 | First role lookup issues `.ASPXROLES`; later requests avoid a store fetch |
| R-FA7 | Authenticated protected request returns 200 |
| R-FA8 | Profile persists and switches identity after sign-in |
| R-FA9 | Output-cached page repeats body and emits public expiry headers |

Role-cookie readings against shipped 4.8.1 binaries:

| ID | Result |
| --- | --- |
| R-RC1 | Cookie-form `RolePrincipal` omits base claims state; missing entries are accepted |
| R-RC2/R-RC3 | Framework stores role-claim iterators unevaluated in `ExternalClaims` |
| R-RC4 | `ClaimsIdentity.Clone()` drops external claims |
| R-RC5 | General serialized claims identities require unsupported BinaryFormatter reconstruction |

## Open

- Add an `apps/` sample that registers, signs in, checks a role, and signs out
  against real `aspnetdb` on all supported platforms.
- Decide whether swallowed ticket-restoration failures need a diagnostic that
  does not report ordinary tampering.

## Done when

The SQL-provider journey has a Framework baseline and three-platform evidence;
ticket diagnostics, if added, distinguish configuration/platform failures from
invalid client cookies.
