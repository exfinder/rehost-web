# Forms authentication, roles, profile, anonymous identity

Scope: registering Framework's authentication modules in the shipped root
configuration and proving the sign-in journey. The module collection itself is
owned by [shipped HTTP modules](shipped-http-modules.md).

## Why nothing works today

`Security/` and `Profile/` are imported and compiled — no `Compile Remove`
touches either tree (`Rehost.WebForms.Runtime.csproj:18-28`). Nothing runs
them. The shipped root `<httpModules>` carries four entries
(`configs/rehost-webforms.web.config:184-189`) and none of this slice's, where
Framework registers `FormsAuthentication`, `RoleManager`,
`AnonymousIdentification`, and `Profile`
(`third_party/microsoft/framework-config/web.config:232-239`).

`DefaultAuthenticationModule` is registered implicitly
(`Configuration/HttpModulesSection.cs:57`), so requests do carry an anonymous
principal and URL authorization works against it. The visible failure is
narrower: the `.ASPXAUTH` cookie is never read, `Request.IsAuthenticated` is
always false, and a denied request ends as a bare 401 instead of the redirect
to `loginUrl`, because that conversion lives in `FormsAuthenticationModule`
(`Security/FormsAuthenticationModule.cs:76-82`). Silent, with no diagnostic.

The shipped `machine.config` also declares the five section handlers
(`:14,15,32,36,38`) while shipping no providers and no `LocalSqlServer`
connection string, where Framework ships both
(`framework-config/machine.config:153,244-261`).

## Scope

In:

- registering `FormsAuthentication`, `RoleManager`, `Profile`,
  `AnonymousIdentification`, and `OutputCache` in the shipped root
  `<httpModules>` at Framework positions;
- carrying Framework's `<membership>`, `<profile>`, `<roleManager>` provider
  entries and the `LocalSqlServer` connection string, with type names
  retargeted to `Rehost.WebForms.Runtime`;
- refusing activation when `<authentication mode="Windows">` is configured;
- behavior coverage for the journey below over a provider supplied by the
  application.

Out:

- `WindowsAuthentication`, `FileAuthorization`, and `PassportAuthentication`
  registration. The first cannot load off Windows
  (`Security/WindowsAuthenticationModule.cs:34` builds a `WindowsIdentity` in a
  static field), the second can never act because no request carries a Windows
  login, and the third talks to a service that no longer exists.
- Microsoft's SQL providers against a real `aspnetdb`. That needs a database and
  is evidenced by a sample application, not by a standing test.
- Persisting the auto-generated machine key. Sign-ins do not survive a restart
  and cannot span instances unless the application configures a literal
  `<machineKey>` (ledger P15).

## Framework readings

Taken 2026-08-22 on winbox, IIS Express 10.0 over .NET Framework 4.8.1, against
an application whose membership, role, and profile providers are its own
in-memory classes. Literal `<machineKey>` (the `fixtures/farm` pair),
`cookieless="UseCookies"`, `roleManager` enabled with `cacheRolesInCookie`,
`anonymousIdentification` enabled, one profile property with
`allowAnonymous="true"`.

| # | Step | Reading |
| --- | --- | --- |
| R-FA1 | Anonymous request to a page under `<deny users="?"/>` | `302 Found`, `Cache-Control: private`, `Location: /Login.aspx?ReturnUrl=%2fSecret%2fDefault.aspx`. The escapes are lower-case hex. Body is Framework's `Object moved` page, 163 bytes |
| R-FA2 | Same response's cookie | `.ASPXANONYMOUS=<base64url>; expires=<now + 70 days>; path=/; HttpOnly`. No `SameSite`. The anonymous identity is issued on the redirect itself |
| R-FA3 | Anonymous request to a public page | `User.Identity` is `GenericIdentity`, `User` is `RolePrincipal` — the role module wraps even the anonymous principal. `Profile.UserName` is the anonymous GUID |
| R-FA4 | `Membership.ValidateUser` then `FormsAuthentication.SetAuthCookie` | `.ASPXAUTH=<upper-case hex>; path=/; HttpOnly; SameSite=Lax`. No `expires`, so a session cookie. `SameSite` is present here and on no other cookie in the journey |
| R-FA5 | Next request carrying `.ASPXAUTH` | `Request.IsAuthenticated` true, `User.Identity` is `FormsIdentity`, `Identity.Name` is the signed-in name |
| R-FA6 | Same response's role cookie | `.ASPXROLES=<base64url>; path=/; HttpOnly`. No `expires`, no `SameSite`. Written on the first request that consults roles, not at sign-in |
| R-FA7 | Protected page carrying `.ASPXAUTH` | `200`, page renders, `User.Identity.Name` is the signed-in name |
| R-FA8 | Profile value saved, then read on a later request | Value survives. `Profile.UserName` switches from the anonymous GUID to the signed-in name once authenticated |
| R-FA9 | Page carrying `<%@ OutputCache Duration="30" VaryByParam="none" %>`, requested twice | Identical body both times. `Cache-Control: public`, `Expires` at request time plus the duration, `Last-Modified` at first render |

The fixture, providers, and the script that produced this are not committed;
the journey is reproducible from the table above.

## Role-cookie readings

Taken on winbox against shipped 4.8.1 binaries: `mscorlib.dll` and `System.dll`
from `Framework64\v4.0.30319`, `System.Web.dll` from the GAC. Decompiled with
`ilspycmd`, and R-RC1/R-RC3 also run as a probe against the live runtime.

| # | Question | Reading |
| --- | --- | --- |
| R-RC1 | `ClaimsPrincipal(SerializationInfo, StreamingContext)` given only the eight entries `RolePrincipal.GetObjectData` writes for a cookie | No throw, `Identities` and `Claims` both empty. `Deserialize` walks `SerializationInfo`'s enumerator and matches two names, `System.Security.ClaimsPrincipal.Identities` and `.Version`; anything else is ignored and absence is not an error |
| R-RC2 | Where `DynamicRoleClaimProvider` lives | `System.dll`, not `mscorlib`. Body is one line, `claimsIdentity.ExternalClaims.Add(claims)`, so the sequence is parked unevaluated |
| R-RC3 | `ClaimsIdentity.ExternalClaims` | `internal Collection<IEnumerable<Claim>>`, reached from `System.dll` through `[FriendAccessAllowed]`. Parking an iterator does not walk it; `Claims` yields instance claims then walks the externals, re-walking on every enumeration. `HasClaim` and `ClaimsPrincipal.IsInRole` see them |
| R-RC4 | `ClaimsIdentity.Clone()` and external claims | Dropped. `Clone` builds from `m_instanceClaims` and copies the scalar fields; it never reads `m_externalClaims` |
| R-RC5 | `ClaimsPrincipal.DeserializeIdentities` | Reads each identity with `BinaryFormatter`, which is why a payload carrying `ClaimsPrincipal` state cannot be rebuilt here: `ClaimsIdentity`'s serialization ctor is stubbed the same way `ClaimsPrincipal`'s is |

## Done when

- The five modules are registered and the four behaviors in R-FA1, R-FA4/R-FA5,
  R-FA6, and R-FA8 are covered by standing scenario tests, plus R-FA9 for
  caching.
- `<authentication mode="Windows">` is refused at activation, with a test.
- A sample application under `apps/` completes register, sign in, role check,
  and sign out through the built-in login controls against SQL Server, with its
  own Framework baseline, on all three operating systems.
- The compatibility map states partial support and names what is unproven.
