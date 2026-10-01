# Wingtip Toys development notes

For setup and things to try, see the [running guide](README.md).

Microsoft's Wingtip Toys tutorial store — an ASP.NET 4.5.2 Web Application
Project with EF6 Code First, Identity 2.x over OWIN, a session-keyed
database-backed shopping cart, role-gated administration and a PayPal Express
checkout — running on the ported runtime from packages, against a containerized
SQL Server. The Milestone 3 application.

Nothing here is a support claim; [`docs/dev/compatibility.md`](../../docs/dev/compatibility.md)
remains the only one. The pre-import analysis is
[`docs/dev/research/wingtiptoys-portability.md`](../../docs/dev/research/wingtiptoys-portability.md);
this file records what actually happened when it ran.

## Provenance

The tutorial's sample download no longer resolves: the `fwlink` in
`dotnet/AspNetDocs` redirects to a **404** on learn.microsoft.com, and no
Microsoft-owned GitHub organisation carries the project. What survives is the
Microsoft-published ZIP itself, captured by the Internet Archive from the MSDN
Code Gallery before retirement. **That archive capture is the trust anchor —
there is no publisher digest to verify against.**

| Property | Value |
| --- | --- |
| Gallery entry | `Getting-Started-with-221c01f5`, "Getting Started with ASP.NET 4.5 Web Forms and Visual Studio 2013 - Wingtip Toys" |
| Author / license | Erik Reitan (Microsoft); Apache License 2.0 (`license.rtf` in the package) |
| Archived URL | `https://code.msdn.microsoft.com/Getting-Started-with-221c01f5/file/107941/11/Getting%20Started%20with%20ASP.NET%204.5%20Web%20Forms%20and%20Visual%20Studio%202013%20-%20Wingtip%20Toys.zip` |
| Snapshot | `https://web.archive.org/web/20170710030442id_/…` — capture 2017-07-10T03:04:42Z |
| File version | 11 of `fileId 107941`, the newest the archive holds; gallery "Updated" 2016-01-07, matching every internal timestamp |
| Bytes / SHA-256 | 17,518,410 / `3b8760a509118992d2b8aedfb95422160262d2c8b342e2a926a3c754a4d67468` |
| Retrieved | 2026-08-29 |

Imported from `C#/WingtipToys` inside that archive, minus `bin/`, `obj/`,
`packages/`, `.csproj.user` and the four `App_Data/*.mdf|ldf` LocalDb files.
`App_Data/ErrorLog.txt` is kept: `Logic/ExceptionUtility.cs` appends to it.

## Layout

| Folder | Role |
| --- | --- |
| `WingtipToys/` | The frozen .NET Framework 4.5.2 WAP. Never modified; byte-identical to the archive. |
| `WingtipToys.App/` | The port of the app assembly: compiles the legacy folder's `*.cs` into `WingtipToys.dll`, plus the `jquery`/`bootstrap` script-mapping shim. |
| `WingtipToys.Host/` | The process: a ~20-line Kestrel host, the app's own `Web.Rehost.config`, and the local PayPal NVP responder the checkout journey runs against. |
| `System.Net.Http.WebRequest/` | A 15-line stand-in for the Framework façade Katana's Google middleware demands. See below — this is the one thing the analysis did not predict. |

## Commands

```text
docker run -d --name rehost-wingtip-sql -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='Rehost!Dev2026' -p 14333:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

dotnet build apps/WingtipToys/WingtipToys.slnx
dotnet run --project apps/WingtipToys/WingtipToys.Host
# http://127.0.0.1:5085/ (add `-- --urls <url>` to change)

apps/WingtipToys/smoke.sh                    # against the default URL
apps/WingtipToys/smoke.sh http://127.0.0.1:5085

docker rm -f rehost-wingtip-sql              # when done
```

The Linux round joins the smoke container to a SQL container that listens on
14333 in-container, so the staged connection string works unchanged:

```text
docker run -d --name rehost-wingtip-sql-linux -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='Rehost!Dev2026' -e MSSQL_TCP_PORT=14333 \
  mcr.microsoft.com/mssql/server:2022-latest
SMOKE_DOCKER_ARGS='--network container:rehost-wingtip-sql-linux' \
  eng/app-linux-smoke.sh WingtipToys 5085
docker rm -f rehost-wingtip-sql-linux
```

Both databases are created and seeded by EF on the first request — nothing
pre-creates them, and a fresh container plus a fresh host is the validated
starting state. On an Apple-silicon machine the amd64 image runs emulated; the
first request takes a few seconds while `DropCreateDatabaseIfModelChanges` and
`RoleActions.AddUserAndRole` build both schemas.

`smoke.sh` is bash + curl only, and uses no bash-4 builtins, so macOS (still
bash 3.2), Linux and Git bash on Windows all run it. It walks the full journey:
anonymous home with the database-seeded category menu, the two folder
authorization gates, register, log off, log back in, the product list over
Friendly URLs, both `MapPageRoute` routes, add-to-cart twice, the cart
`GridView`, an Update postback that changes one row's quantity, the master
page's cart count following it, checkout through `CheckoutReview` and
`CheckoutComplete` to the written order and the emptied cart, the `customErrors`
404 row, the seeded `canEdit` user reaching the admin page, static assets, and
both production bundles asserted on their minified content.

It needs no network beyond the host and the database container.

## packages.config → PackageReference

Packages built on `System.Web` take the Rehost counterpart the
[package mapping](../../docs/migration.md#package-mapping) names; the table records this
application's decisions.

| `packages.config` | Here | Note |
| --- | --- | --- |
| `Microsoft.Owin.Host.SystemWeb` 2.1.0 | Rehost counterpart | Already landed with the Identity application; **no new recompile** |
| `Owin`, `Microsoft.Owin`, `.Security`, `.Security.Cookies`, `.Security.OAuth`, `.Security.Google`, `.Facebook`, `.Twitter`, `.MicrosoftAccount` 2.1.0 | same packages from nuget.org at 4.2.3 | Consumed as shipped under `NU1701`, the Identity-application precedent — with one exception, below |
| `Microsoft.AspNet.Identity.Core`, `.Owin`, `.EntityFramework` 2.1.0 | same packages at 2.2.4 | Pure managed |
| `EntityFramework` 6.1.1 | same package at 6.5.2 | 6.3+ ships `netstandard2.1` |
| `Microsoft.AspNet.Web.Optimization`, `.WebForms`, `WebGrease`, `Antlr`, `Newtonsoft.Json` | Rehost counterparts | The `<controls>` assembly rewrite is in the default XDT |
| `Microsoft.AspNet.FriendlyUrls`, `.Core` | Rehost counterpart | Genuinely active here: `RedirectMode.Permanent` |
| `Microsoft.AspNet.ScriptManager.MSAjax`, `.WebForms` | Rehost counterparts | `Site.Master` asks for `MsAjaxBundle` and `WebFormsBundle` |
| `AspNet.ScriptManager.jQuery`, `.bootstrap` | `WingtipToys.App/PreApplicationStartCode.cs` | Names only, so the definitions are re-registered by hand; see below |
| `elmah`, `elmah.corelibrary` 1.2.2 | dropped by XDT | Binds Framework's strong-named `System.Web`, no portable build, and no application code references it |
| `Microsoft.AspNet.Providers.Core` | dropped | Named only as `<sessionState customProvider>`, which `mode="InProc"` never resolves — parses and activates exactly as on Framework |
| `Microsoft.Web.Infrastructure` | dropped | Katana 4.x calls `HttpApplication.RegisterModule` directly |
| `jQuery`, `bootstrap`, `Modernizr`, `Respond` | unchanged content | Committed under `Scripts/`, `Content/`, `fonts/` |
| — | `System.Data.SqlClient` 4.9.1 | The provider factory EF6 needs off Framework |

**Zero library recompiles.** This is the first application whose entire
third-party closure was already solved: the App/Host pair built and the home
page rendered on the first attempt after the one unanticipated fixture below.

## The one unanticipated blocker: `System.Net.Http.WebRequest`

`App_Start/Startup.Auth.cs:62-66` calls `app.UseGoogleAuthentication(...)`
unconditionally with a placeholder client id. The analysis expected that to
cost a rendered button that fails at Google. It costs more than that:

```text
FileNotFoundException: Could not load file or assembly
'System.Net.Http.WebRequest, Version=4.0.0.0, PublicKeyToken=b03f5f7f11d50a3a'
   Microsoft.Owin.Security.Google.GoogleOAuth2AuthenticationMiddleware.ResolveHttpMessageHandler
   …
   Microsoft.Owin.Host.SystemWeb.OwinHttpModule.Init
```

`ResolveHttpMessageHandler` constructs `new WebRequestHandler()` before
consulting anything, and `WebRequestHandler` lives in a Framework façade
assembly that .NET 10 does not carry. `Microsoft.Owin.Security.Google` ships
`net45` only — Katana 4.x never produced a `netstandard` build of it — so there
is no package version that avoids this. The failure is at OWIN pipeline
construction inside `HttpApplication.InitModules`, so **every request 500s and
no page renders at all**.

`System.Net.Http.WebRequest/` supplies the identity and the one type, deriving
from `HttpClientHandler` and forwarding the certificate callback that Katana
would set if `BackchannelCertificateValidator` were configured (it is not here).
That is the whole assembly. With it, Google authentication is back to the
boundary the analysis predicted: the button renders, the challenge fails at
Google.

This is an application-side fixture, in the shape of
`apps/eShopLegacyWebForms/Autofac.Integration.Web`, not port surface: nothing in
`src/` changed for this bring-up. **It is also the first evidence that
"consumable from nuget.org after recompile" is not a safe classification for the
Katana security providers** — the Identity application carries the same package
reference and never met this, because it never invokes the middleware.

## web.config

`WingtipToys.Host/Web.Rehost.config` replaces the package default wholesale, so
it repeats the default's `<runtime>` removal and Optimization `<controls>`
retarget first. (The default's `<system.codedom>` removal is *not* repeated:
this application predates the DotNetCompilerPlatform template change and has no
such element, so the rule only produces an XDT warning.) Three app-specific
edits:

| Edit | Justifying failure |
| --- | --- |
| Remove the three `Elmah.*` rows from `<system.webServer><modules>` | A module row whose type will not load fails its URLs with the entry named (MH22a). `Elmah.dll` binds Framework's strong-named `System.Web`. |
| `DefaultConnection` → `Data Source=127.0.0.1,14333;Initial Catalog=aspnet-WingtipToys;…` | `(LocalDb)\v11.0` is a Windows-only engine. |
| `WingtipToys` → `Data Source=127.0.0.1,14333;Initial Catalog=WingtipToys;…` | Same, plus `AttachDbFilename=\|DataDirectory\|\wingtiptoys.mdf` has no container equivalent, so the file store becomes a named catalog rather than a renamed data source. |

**`<customErrors>` is as authored.** It was transformed to `mode="Off"` during
bring-up — the `System.Net.Http.WebRequest` diagnosis above depended on that —
and the transform is gone: the staged `web.config` carries the application's own
`mode="On"`, its `defaultRedirect`, and its 404 row. `smoke.sh` asserts that row,
and asserts a page-specific marker wherever a step could otherwise have passed on
`ErrorPage.aspx`'s copy of the master page.

**The rest of the ELMAH surface needed no transform.** The `elmah`
`<configSections>` sectionGroup with its four `Elmah.*` handler types, the
`<elmah><security/>` element, and the `<location path="elmah.axd">` block with
its `<httpHandlers>`/`<handlers>` entries are all still in the staged
`web.config` and the application runs. Section-handler types resolve lazily, and
folder handler lists are built by discovering folder `Web.config` files rather
than from root `<location>` blocks. The analysis left this unverified and
proposed dropping the whole surface; measurement says three rows is enough.

**`<entityFramework><defaultConnectionFactory>` was left as authored** —
`LocalDbConnectionFactory` from `EntityFramework` 6.5.2. Both contexts name a
connection string explicitly, so the factory is never consulted, and the type
loads fine off Windows. It is dead configuration, not a portability problem.

Nothing else needed a transform: `<sessionState>` naming an unresolvable
`System.Web.Providers` type, the unhonored
`<modules><remove name="FormsAuthentication" />`, the `<httpModules>` classic
block waived by `<validation validateIntegratedModeConfiguration="false" />`,
and the three `<membership>`/`<profile>`/`<roleManager>` `<clear />` blocks all
parse and activate as they do on Framework.

## `jquery` was provably blocking, not speculative

`<httpRuntime targetFramework="4.5.2" />` makes
`ValidationSettings.UnobtrusiveValidationMode` default to `WebForms`, so every
validator calls `ClientScriptManager.EnsureJqueryRegistered`, which throws when
no `jquery` `ScriptResourceMapping` exists. `Account/Register.aspx` and
`Account/Login.aspx` — the journey's first two pages — carry
`RequiredFieldValidator`s, and the definition came from
`AspNet.ScriptManager.jQuery`, which the port replaces with names only.
`WingtipToys.App/PreApplicationStartCode.cs` registers `jquery` and `bootstrap`
against the files actually in `Scripts/` (1.10.2 and 3.0.0), the eShop pattern
verbatim.

## The local PayPal NVP responder

`Logic/PayPalFunctions.cs` hard-codes everything: `bSandbox` is a `const bool`,
`pEndPointURL_SB` is a literal, and `HttpCall` at `:187` does
`(HttpWebRequest)WebRequest.Create(url)` with no configuration read anywhere in
the class. There is no application-level seam — no app setting, no XDT-reachable
element — so the endpoint cannot be redirected through configuration.

The seam that does exist is one level down, in the BCL:
`WebRequest.RegisterPrefix` is honored by .NET 10's `WebRequest.Create`, the
prefix list is ordered longest-first so an endpoint-specific prefix outranks the
built-in `https:` entry, and a creator that returns
`WebRequest.Create("http://127.0.0.1:<port>/nvp")` still returns an
`HttpWebRequest`, so the frozen cast holds.

`WingtipToys.Host/PayPalNvpResponder.cs` starts a second Kestrel instance on
`127.0.0.1:0` before the application host and registers that prefix for
`https://api-3t.sandbox.paypal.com`. It answers the three methods the checkout
pages call — `SetExpressCheckout` issues a token and remembers the order total
verbatim, `GetExpressCheckoutDetails` returns that same `AMT` string plus a
shipping address, `DoExpressCheckoutPayment` returns a transaction id — and
`ACK=Failure` with real NVP error fields for anything else. Echoing the amount
string rather than re-formatting it is what keeps `CheckoutReview`'s
`Convert.ToDecimal` mismatch guard passing under any host culture.

Nothing about it is a port claim. It is an application-side fixture, the shape
of `System.Net.Http.WebRequest/` above: it listens on loopback with an ephemeral
port, needs no hosts entry and no outbound access, and touches no frozen source.
The real sandbox is never contacted — with the tutorial's placeholder
credentials it could only ever answer `10002 Security error`.

## Verified working

In rough order of how unproven each was going in.

- **The whole checkout, through the order write and `EmptyCart`.** Against the
  responder above: `CheckoutStart` hands off with the issued token,
  `CheckoutReview` binds the returned shipping address into a `DetailsView`,
  passes its amount-mismatch guard, writes one `Order` and one `OrderDetail` per
  line with a `SaveChanges` each, and `CheckoutComplete` re-reads that order by
  id, stamps the transaction id, and empties the cart. Five session keys cross
  four requests and an external redirect. This is the surface the milestone
  exists for and nothing here had run before.
- **`customErrors mode="On"` end to end, for the 404 row.** A missing page
  redirects to the authored `ErrorPage.aspx?msg=404&handler=…` URL, Friendly
  URLs then drops the extension, and `ErrorPage.aspx` renders the friendly
  message and — `Request.IsLocal` being true for a loopback client — the
  detailed panel. The `defaultRedirect` branch for an unhandled exception is
  still unassessed.
- **Production-mode Optimization on the first render of every page.**
  `BundleConfig.cs:39` sets `EnableOptimizations = true` unconditionally,
  overriding `debug="true"`. `~/bundles/modernizr` returns 11 KB of minified
  Modernizr and `~/Content/css` returns 113 KB of combined, minified
  `bootstrap.css` + `Site.css`. The backlog's unvalidated WebGrease path is now
  exercised against real inputs, on the critical path of every page.
- **The `Bundle.config` manifest.** `~/Content/css` exists *only* in
  `Bundle.config` and reaches the runtime through `BundleTable.EnsureBundleSetup`
  reading `~/bundle.config` — a lowercase virtual path against an uppercase file
  on disk. No prior application exercised it; it resolves, and the master page's
  `<webopt:bundlereference>` renders the versioned link.
- **Role-based `<authorization>` against an Identity-claims principal.**
  `Admin/Web.config` allows `roles="canEdit"` with no `RoleProvider` configured
  anywhere. The `canEditUser@wingtiptoys.com` account that
  `Application_Start` seeds signs in and reaches `/Admin/AdminPage` with 200,
  and `Site.Master.cs:74`'s `IsInRole("canEdit")` reveals the Admin link. The
  analysis marked this unverified and the map calls it Partial.
- **URL authorization by anonymity on a folder,** including the
  `<location path="Manage.aspx">` nested inside `Account/Web.config`.
  `/Checkout` and `/Admin` both 302 anonymous callers to the absolute login URL.
- **`Application_Start` creating and seeding two databases before the first
  page.** `Database.SetInitializer` plus `RoleActions.AddUserAndRole()` reaching
  EF directly, not through the OWIN per-request factory. The Identity
  application's recorded hazard — reaching EF before `HostingEnvironment`
  initialises — does not apply, because `Application_Start` runs after
  activation.
- **The session-keyed, database-backed cart across a sign-in.** The cart id
  starts as a session GUID, `MigrateCart` rewrites the rows and the session key
  on register and again on login, and `Site.Master.cs:82-86` re-queries the
  count in `Page_PreRender` on every render.
- **Model binding on `GridView`, `FormView`, `DetailsView` and `DropDownList`,**
  with `ExtractValuesFromCell` over `GridViewRow` in the cart's Update postback.
  All four were Unassessed on the map.
- **Two `MapPageRoute` routes alongside active Friendly URLs**, with Friendly
  URLs registered first, plus `[RouteData]`/`[QueryString]` value providers on
  `ProductDetails`. `/Category/Rockets` and `/Product/Convertible%20Car` both
  bind, and `GetRouteUrl` in markup emits them.
- **`Server.MapPath` on a trailing-slash virtual path returns a trailing
  separator off Windows.** The analysis flagged
  `Server.MapPath("~/Catalog/Images/") + FileName` as an unverified concatenation
  hazard. An admin `FileUpload` post lands both `SaveAs` calls — `Images/x.png`
  and `Images/Thumbs/x.png` — at the right paths on macOS.
- **Event validation.** A hand-built multipart post with an unregistered
  `DropDownList` value is rejected with the Framework's own
  `Invalid postback or callback argument` — the ordinary correct behavior, worth
  recording because the smoke's postbacks all had to satisfy it.
- **The `MsAjax`/`MSAjax` casing mismatch in `BundleConfig.cs`** folds, under
  production bundling, on a case-insensitive filesystem. Untested on a
  case-sensitive one.

## Boundaries

- **Checkout is driven against a fixture, not PayPal.** The tutorial's
  placeholder credentials (`Logic/PayPalFunctions.cs:36-38`) make the real
  sandbox unusable, so what the journey proves is the application's own
  behavior around the NVP boundary, not interoperability with PayPal. The
  hard-coded `https://localhost:44300/Checkout/CheckoutReview.aspx` return URL
  (`:62-63`) is still sent and still ignored: the browser is redirected to
  `www.sandbox.paypal.com` and the smoke resumes the journey at
  `CheckoutReview` on this host, exactly as the return URL would have. The
  `CheckoutCancel` path is not walked.
- **Google external login fails at Google.** The client id is
  `000000000000.apps.googleusercontent.com`. The button renders on Login and
  Register; clicking it issues an OWIN challenge that Google rejects. Commenting
  it out would be an application-source change the frozen-tree rule forbids.
- **`customErrors` is restored, but only its 404 row is assessed.** The staged
  config is the application's own `mode="On"`. What runs is the `<error
  statusCode="404">` redirect and `ErrorPage.aspx` behind it. `Application_Error`
  → `Server.Transfer("ErrorPage.aspx")` never fires here — `Global.asax.cs:55`
  only transfers for an `HttpUnhandledException` with an inner exception, and
  the journey raises none — so `Server.GetLastError`, `Server.ClearError` on a
  real exception, and the `defaultRedirect` branch remain unassessed. Note that
  the transfer target is relative, so from a page under `Checkout/` it would
  resolve to a `Checkout/ErrorPage.aspx` that does not exist.
- **LocalDb is out of contract,** as in every prior application. Here it is
  reached on *every* page, so the connection-string swap is the one
  application-visible edit that has to happen before anything renders at all.
  `AttachDbFilename` makes the second string a genuine topology change, not a
  rename: a named catalog on a server instead of an attached file.
- **Currency rendering follows the host machine's culture.** The application
  sets no `<globalization>`, and every price goes through
  `String.Format("{0:c}")`. On an `en-US` machine the cart reads `$145.45`;
  other cultures change the symbol and the decimal separator, so `smoke.sh`
  compares digits only. Under a comma-decimal culture the admin page's
  `double.Parse("1.00")` throws. That is an application assumption, not a port
  defect.
- **`<machineKey>` is auto-generated.** The application declares none, so keys
  are per-application and host-resolvable (ADR 0010); sign-ins survive a
  restart, but two instances cannot share them.

## What looked like two latent defects

The pre-import analysis called out
`Checkout/CheckoutComplete.aspx.cs:47-49` as fatal: it compares
`Session["currentOrderId"]` (an `int`) to `string.Empty`, and then reads
`Session["currentOrderID"]` — different casing — which was read as a different
key yielding `Convert.ToInt32(null)` and an order lookup for id 0. Running it
says otherwise. The comparison is indeed always true, but harmlessly so, and
**ASP.NET session keys are case-insensitive**
(`SessionStateItemCollection` is built over
`Misc.CaseInsensitiveInvariantKeyComparer`), so the second read returns the id
`CheckoutReview` stored. `_db.Orders.Single(…)` finds the row, the transaction
id is saved, and the cart empties — the smoke asserts all three.

`Logic/ShoppingCartActions.cs:99-107` (`GetCart`) still returns a context it has
already disposed. Nothing calls it.

## Rows this evidence backs

The claims themselves live in
[`docs/dev/compatibility.md`](../../docs/dev/compatibility.md), which names this
application in five rows.

| Row | What Wingtip Toys adds |
| --- | --- |
| Web Application Project model | A frozen WAP tree beyond the two templates, and the stateful commerce journey against SQL Server |
| Optimization/WebForms and WebGrease | Production combination and minification over real inputs, including a bundle declared only in `Bundle.config` |
| `customErrors` redirects | The 404 row, and the application's own error page behind it |
| Session state | InProc session across an OWIN sign-in and a four-request checkout |
| Forms authentication, roles, profiles, anonymous identity | Role checks resolved from an Identity claims principal with no `RoleProvider` |

## Not exercised here

The admin Remove-product path, `Checkout/CheckoutCancel`, `ViewSwitcher.ascx`
and `Site.Mobile.Master`, `Account/Manage` and the two-factor and external-login
pages, the `customErrors` `defaultRedirect` branch and the `Application_Error`
transfer behind it, and the `elmah.axd` handler block that the XDT deliberately
left in place.
