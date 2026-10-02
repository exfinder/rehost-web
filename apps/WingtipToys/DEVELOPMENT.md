# Wingtip Toys development

[Running guide](README.md) · [Imported sources](../../docs/dev/sources.md)

`.App` compiles the legacy WAP and registers jquery/bootstrap names against its
physical scripts. `.Host` owns Kestrel, XDT and the local PayPal responder.

## WebRequest facade

`System.Net.Http.WebRequest` supplies the Framework facade Google middleware
constructs during pipeline initialization, even with placeholder credentials.
Its WebRequestHandler derives from HttpClientHandler and forwards certificate
validation. This shim is an application fixture, not general runtime surface.

## web.config

- Replace both LocalDb connections with named SQL Server catalogs. EF creates
  and seeds both databases on first request; the unused LocalDb default factory
  remains configured because both contexts name their connection strings.
- Remove ELMAH module rows and the `location path="elmah.axd"` handler block.
  The lazy, unused ELMAH section declarations remain.
- Preserve authored `customErrors mode="On"`, its default redirect and 404 row.
- The merged module list honors `remove name="FormsAuthentication"`. Classic
  modules remain waived by `validateIntegratedModeConfiguration=false`.
- InProc session never constructs the unavailable custom provider named by the
  legacy configuration. Membership/profile/role provider collections are cleared;
  authorization uses Identity role claims, not SQL role providers.
- Machine keys persist per application. Multiple instances need shared explicit
  keys; see [machine-key setup](../../docs/migration.md#machine-keys).

## Checkout fixture

PayPalFunctions hard-codes its sandbox endpoint. The Host registers an endpoint
prefix through WebRequest.RegisterPrefix and redirects it to a loopback responder
on an ephemeral port, preserving the frozen HttpWebRequest cast. The responder
implements the three checkout NVP methods and echoes the amount string unchanged
so the application's culture-sensitive mismatch guard sees the same value.
The journey resumes CheckoutReview locally; it does not use the tutorial's
hard-coded HTTPS return URL or real PayPal credentials.

Google login likewise has placeholder credentials. Currency follows host culture;
the admin page's `double.Parse("1.00")` assumes a compatible decimal separator.
These are application constraints, not runtime portability claims.

## Open application scope

CheckoutCancel, product removal, mobile views, account management, two-factor
and external-login flows remain unassessed. The unhandled-error default redirect
and Application_Error transfer are also unassessed; its relative ErrorPage.aspx
would resolve under the failing page's folder. Runtime support lives in
[compatibility](../../docs/dev/compatibility.md).

## Linux workflow

The smoke container shares the SQL container's network namespace, with SQL Server
listening at the connection string's port. Start with a fresh database for a clean
initialization round.

```text
docker run -d --name rehost-wingtip-sql-linux -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='Rehost!Dev2026' -e MSSQL_TCP_PORT=14333 \
  mcr.microsoft.com/mssql/server:2022-latest
SMOKE_DOCKER_ARGS='--network container:rehost-wingtip-sql-linux' \
  eng/app-linux-smoke.sh WingtipToys 5085
docker rm -f rehost-wingtip-sql-linux
```
