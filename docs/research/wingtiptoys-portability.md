# Wingtip Toys portability analysis

Preliminary, pre-import analysis of Microsoft's Wingtip Toys — the Milestone 3
application. Source read at `../../../WingtipToys/C#/WingtipToys` (read-only
extraction, nothing modified); paths below are relative to that folder unless
stated. Nothing here is a support claim; the
[compatibility map](../compatibility.md) remains the only one. Statements the
current evidence cannot decide are marked **unverified**.

## Source and authenticity

The tutorial series ("Getting Started with ASP.NET 4.5 Web Forms and Visual
Studio 2013", Erik Reitan) survives on learn.microsoft.com, but its sample
download does not. `introduction-and-overview.md` in `dotnet/AspNetDocs` still
points at `https://go.microsoft.com/fwlink/?LinkID=389434&clcid=0x409`; that
fwlink now redirects to
`https://learn.microsoft.com/en-us/Getting-Started-with-221c01f5?cdn_id=2013-12-16-001`,
which returns **404**. No Microsoft-owned GitHub organisation carries the
project: `microsoftarchive/msdn-code-gallery-*` has no Wingtip entry, and
`aspnet/samples` and `dotnet/AspNetDocs` carry the prose only. Every GitHub
`WingtipToys` repository is an individual's fork of a tutorial exercise.

What does survive is the Microsoft-published ZIP itself, captured by the
Internet Archive from the MSDN Code Gallery before retirement.

| Property | Value |
| --- | --- |
| Gallery entry | `Getting-Started-with-221c01f5`, "Getting Started with ASP.NET 4.5 Web Forms and Visual Studio 2013 - Wingtip Toys" |
| Author / license | Erik Reitan (Microsoft); Apache License 2.0 (`license.rtf` in the package) |
| Gallery "Updated" | 1/7/2016 (`description.html`); every file in the archive carries the timestamp `2016-01-07 11:51` |
| Archived URL | `https://code.msdn.microsoft.com/Getting-Started-with-221c01f5/file/107941/11/Getting%20Started%20with%20ASP.NET%204.5%20Web%20Forms%20and%20Visual%20Studio%202013%20-%20Wingtip%20Toys.zip` |
| Snapshot | `https://web.archive.org/web/20170710030442id_/…` — capture 2017-07-10T03:04:42Z |
| Retrieved | 2026-08-29 |
| Bytes / SHA-256 | 17,518,410 / `3b8760a509118992d2b8aedfb95422160262d2c8b342e2a926a3c754a4d67468` |
| Contents | 592 entries: `description.html`, `license.rtf`, `C#/WingtipToys.sln`, `C#/WingtipToys/`, `C#/WingtipToys-Assets/`, `C#/packages/` |
| Extracted to | `/Users/vm/repos/WingtipToys` |

**Authenticity: high, with one caveat.** The bytes come from a `microsoft.com`
URL, wrapped in the gallery's own `description.html`/`license.rtf` metadata,
with internal timestamps matching the gallery's published update date. This is
file version **11** of `fileId 107941`, the newest the archive holds; the CDX
index also holds v7 (2014-08) and v10 (2015-06) of the same file, plus an older
`fileId 55767` from 2012, which predates the VS 2013 rewrite. No Microsoft
digest survives to hash against, so the trust anchor is the archive's capture of
the Microsoft URL rather than a signature. **This is the decision the user owns
before the tree is frozen** (see open decisions).

**Final-chapter state: yes.** Every artifact the ten-chapter series produces is
present: the EF Code First model and initializer (`Models/`), the shopping cart
(`Logic/ShoppingCartActions.cs`), Identity registration and login (`Account/`),
role-gated administration (`Admin/`, `Logic/RoleActions.cs`), PayPal checkout
(`Checkout/`, `Logic/PayPalFunctions.cs`), and the error-handling chapter's
`ErrorPage.aspx`, `Logic/ExceptionUtility.cs` and ELMAH wiring.

## Application shape

| Property | Value | Source |
| --- | --- | --- |
| Project model | Web Application Project (`{349c5851-…}` guid), `OutputType Library` | `WingtipToys.csproj:11-12` |
| Target framework | `v4.5.2`; `<compilation targetFramework="4.5.2">`, `<httpRuntime targetFramework="4.5.2">` | `csproj:16`, `Web.config:25-26` |
| Build customisations | **None.** `BeforeBuild`/`AfterBuild` are the commented-out template stubs; no `PostBuildEvent`, no custom target, no content generator | `csproj:557-563`; imports at `:536-538` are the stock `Microsoft.CSharp.targets` + `Microsoft.WebApplication.targets` |
| Code-behind | Checked-in `.designer.cs` for every page, master and control; they declare protected fields only | `csproj:282-527`, e.g. `ShoppingCart.aspx.designer.cs:13-22` |
| Pages | 9 top-level `.aspx`, 16 under `Account/`, 5 under `Checkout/`, 1 under `Admin/`, `Site.Master`, `Site.Mobile.Master`, `ViewSwitcher.ascx`, `OpenAuthProviders.ascx`, `Global.asax` | tree |
| Language version | none set; **no `<system.codedom>` at all** — this predates the DotNetCompilerPlatform template change | `Web.config` |
| Hosting metadata | `UseIISExpress`, SSL port 44300, classic-pipeline flag empty | `csproj:17-21` |
| Solution | one project, VS Express 2013 for Web, format 12.00 | `../WingtipToys.sln:3-7` |

Both framework-version thresholds clear the port's requirements: `httpRuntime`
4.5.2 ≥ 4.5, `compilation` 4.5.2 ≥ 4.0. The shape is the closest of the four
applications so far to the frozen Milestone 1 templates — it *is* the VS 2013
"Individual User Accounts" template with commerce pages grafted on, which is why
so much of the closure is already proven.

`App_Data/` ships `wingtiptoys.mdf`/`.ldf` and `aspnet-WingtipToys-*.mdf`/`.ldf`
plus a stale `ErrorLog.txt`. Per the bring-up rule those databases are not
imported; `ErrorLog.txt` is, because `ExceptionUtility` appends to it.

## Dependency inventory and classification

Classes: **A** provided by the port, **B** consumable from nuget.org after
recompile, **C** real gap (no port surface and no consumable package), **D**
Windows- or platform-bound.

### System.Web surface reached by application code

| Surface | Class | Evidence |
| --- | --- | --- |
| Pages, masters, user controls, code-behind, `Global.asax` | A | [map](../compatibility.md): pre-application start / `Global.asax`, `.aspx` GET, master pages and user controls — Supported |
| `Application_Start`, `Application_Error` | A | map: async module events Supported; `Global.asax.cs:17,48` |
| `Server.Transfer` from `Application_Error` | A | map: `Server.Transfer`/`Execute` Supported; `Global.asax.cs:60` |
| `Server.GetLastError` / `ClearError`, `customErrors` redirect, `Request.IsLocal` | A (unverified breadth) | `ErrorPage.aspx.cs:32,49,80`; `Web.config:21-23`. No map row names `customErrors` end to end |
| Friendly URLs `EnableFriendlyUrls` with `RedirectMode.Permanent` | A (Partial) | `App_Start/RouteConfig.cs:13-15`; map: Friendly URLs Partial, redirects exercised |
| `WebFormsFriendlyUrlResolver.IsMobileView`, switch-view route, `Site.Mobile.Master` | A (Partial) | `ViewSwitcher.ascx.cs:23,31`; map: mobile pages/masters and view switching exercised. **Unlike eShop, Friendly URLs is genuinely active here** |
| Classic `MapPageRoute` (2 routes) + `GetRouteUrl` in markup | A (scope unverified) | `Global.asax.cs:36-45`; `Site.Master:93`, `ProductList.aspx:33,41`. `src/System.Web.ReferenceSource/Routing/PageRouteHandler.cs`; map's routing rows are written for Friendly URLs |
| `[RouteData]` and `[QueryString]` model-binding value providers | A (unassessed) | `ProductDetails.aspx.cs:20-21`; `src/System.Web.ReferenceSource/ModelBinding/` |
| `ListView` + `ItemType` + `SelectMethod` (×4) | A | map: `ListView`/`DataPager` closure landed with the Identity application (ledger P69) |
| `GridView`, `FormView`, `DetailsView`, `DropDownList` with `ItemType`/`SelectMethod`/`DeleteMethod` | A (**unassessed**) | `ShoppingCart.aspx:4-6`, `ProductDetails.aspx:4`, `Admin/AdminPage.aspx:11-12,56-57`, `Account/ManageLogins.aspx:13-14`. Map: other controls Unassessed |
| `DataControlField.ExtractValuesFromCell` over `GridViewRow` | A (unassessed) | `ShoppingCart.aspx.cs:81` — the cart's Update postback |
| `LoginView` / `LoginStatus` with `OnLoggingOut` → OWIN sign-out | A | `Site.Master:61-76`; the Identity application's smoke already asserts the expired `.ASPXAUTH` that `LoginStatus` writes |
| `Page.ViewStateUserKey`, anti-XSRF cookie, view state | A | `Site.Master.cs:30,36,58-68`; map: forms/postback/view state Supported |
| 21 `RequiredFieldValidator`, 4 `CompareValidator`, 1 `RegularExpressionValidator`, 7 `ValidationSummary`, 2 `ModelErrorMessage` | A, **with a hard prerequisite** | see the `jquery` mapping gap below |
| `Session[…]` on cart, checkout amount, PayPal token/payer/order | A | `Logic/ShoppingCartActions.cs:61-75`, `ShoppingCart.aspx.cs:96`, `Checkout/CheckoutStart.aspx.cs:18-25`, `CheckoutReview.aspx.cs:23,28,87,109`, `CheckoutComplete.aspx.cs:18,32-34`. Map: session state Partial — InProc works |
| `HttpContext.Current.User.Identity.Name` / `IsInRole` | A (Partial) | `Logic/ShoppingCartActions.cs:63-66`, `Site.Master.cs:74`; map: forms authentication/roles Partial. The role comes from the Identity cookie's claims, not a `RoleProvider` |
| URL authorization by role and by anonymity, in three folder `Web.config` files | A (Partial) | `Admin/Web.config:4-7`, `Checkout/Web.config:4-6`, `Account/Web.config:4-10`; map: managed URL authorization for direct/routed/child config works (MH36) |
| `FileUpload` + `PostedFile.SaveAs` | A | `Admin/AdminPage.aspx.cs:50-52`; map: uploaded-file `SaveAs` Supported |
| `Server.MapPath` for uploads and the error log | A | `Admin/AdminPage.aspx.cs:31`, `Logic/ExceptionUtility.cs:22`; map: path-taking APIs Supported |
| `Response.Redirect`, `<a runat="server" href="~">`, `ResolveUrl` | A | map: path-taking APIs Supported |
| `System.Web.Optimization` `Scripts.Render` + `webopt:bundlereference` | A (Partial), **production mode** | `Site.Master:12,14`; `App_Start/BundleConfig.cs:39` sets `EnableOptimizations = true`. See gaps |
| `Bundle.config` manifest (`~/Content/css`) | A (unverified) | `Bundle.config:3-6`. `BundleTable.Bundles` reads `~/bundle.config` on first access (`src/System.Web.Optimization.ReferenceSource/BundleTable.cs:57-64`) — the *only* source of the site's stylesheet bundle; `BundleConfig.cs` never registers it |
| `ScriptManager` with 4 named and 8 `Assembly="System.Web"` script references | A (Partial) | `Site.Master:20-39`; map: `System.Web.Extensions`/ScriptManager Supported |
| `ConfigurationManager` app settings / connection strings via EF and Identity | A | map: static `ConfigurationManager` inside the application Supported |

### packages.config

| Package | Class | Disposition |
| --- | --- | --- |
| `Microsoft.Owin` 2.1.0, `.Security`, `.Security.Cookies`, `.Security.OAuth`, `.Security.Google`, `.Facebook`, `.Twitter`, `.MicrosoftAccount`, `Owin` 1.0 | B | Consumed from nuget.org at 4.2.3, exactly as `WebFormsIdentityApplication.App` does (`apps/WebFormsIdentityApplication/WebFormsIdentityApplication.App/…csproj:27-36`) |
| `Microsoft.Owin.Host.SystemWeb` 2.1.0 | A | `Rehost.WebForms.Owin.Host.SystemWeb` — the Katana recompile already landed. **No new recompile needed** |
| `Microsoft.AspNet.Identity.Core` / `.Owin` / `.EntityFramework` 2.1.0 | B | Same packages at 2.2.4 from nuget.org, the Identity-application precedent |
| `EntityFramework` 6.1.1 | B | 6.5.2 from nuget.org, the Identity/eShop precedent |
| `Microsoft.AspNet.Web.Optimization` 1.1.3, `.WebForms`, `WebGrease` 1.5.2, `Antlr` 3.4.1.9004, `Newtonsoft.Json` 6.0.3 | A | `Rehost.WebForms.Optimization`, `.Optimization.WebForms`; the `<controls>` assembly rewrite is in the default XDT (`src/Rehost.WebForms.Hosting/build/Web.Rehost.config:13-15`) |
| `Microsoft.AspNet.FriendlyUrls` + `.Core` 1.0.2 | A | `Rehost.WebForms.FriendlyUrls` |
| `Microsoft.AspNet.ScriptManager.MSAjax` / `.WebForms` 5.0.0 | A | `Rehost.WebForms.ScriptManager.Bundles` registers `MsAjaxBundle`, `WebFormsBundle` and the MicrosoftAjax names |
| `AspNet.ScriptManager.jQuery` 1.10.2 | **C, blocking** | Registers the `jquery` `ScriptResourceMapping`. Unregistered here, and unlike eShop the consequence is provably fatal — see gap 1 |
| `AspNet.ScriptManager.bootstrap` 3.0.0 | **C** | Registers `bootstrap`, referenced from `Site.Master:26` on every page. Same shim |
| `Microsoft.AspNet.Providers.Core` 2.0.0 (`System.Web.Providers`) | **C, inert** | Named only as `<sessionState customProvider>`, which `mode="InProc"` never resolves. `WebFormsIdentityApplication` carries the identical line and records that it "parses and activates exactly as it does on Framework" (`apps/WebFormsIdentityApplication/README.md`, web.config section) |
| `elmah` 1.2.2 + `elmah.corelibrary` 1.2.2 | **C, blocking** | `Elmah.dll` binds Microsoft's strong-named `System.Web`; last published 2011, no `netstandard` build, and modern `ElmahCore` is a different API for ASP.NET Core. Registered as three `<modules>` rows. **No application code references Elmah** (a full scan over `*.cs`/`*.aspx`/`*.master`/`*.ascx` returns nothing) |
| `Microsoft.Web.Infrastructure` 1.0.0 | A (dropped) | Same disposition as all three prior applications |
| `jQuery` 1.10.2, `bootstrap` 3.0.0, `Modernizr` 2.6.2, `Respond` 1.2.0 | — | Content only; committed under `Scripts/`, `Content/`, `fonts/` |

Nothing in the GAC `<Reference>` block is reached by application code:
`System.Web.DynamicData`, `System.Web.Entity`, `System.EnterpriseServices`,
`System.Drawing`, `System.Data.DataSetExtensions`, `System.Web.Services` and
`System.Web.ApplicationServices` (`csproj:44-60`) all disappear with the GAC.
The `System.Drawing` reference in particular is the stock template line — a scan
for `System.Drawing`, `Bitmap`, `ToolboxBitmap` and `UITypeEditor` across the
whole tree returns zero hits, so **the AjaxControlToolkit design-time marker
work has no consumer here**.

### Web.config

| Entry | Class | Note |
| --- | --- | --- |
| `<httpRuntime targetFramework="4.5.2" />` (`:26`) | A, with a consequence | Clears the ≥4.5 requirement. It also makes `ValidationSettings.UnobtrusiveValidationMode` default to `WebForms` (`src/System.Web.ReferenceSource/UI/ValidationSettings.cs:20`), which is what makes the missing `jquery` mapping fatal |
| `<compilation debug="true" targetFramework="4.5.2" />` (`:25`) | A | ≥4.0. `debug="true"` would normally put Optimization in expansion mode, but `BundleConfig.cs:39` overrides it |
| `<authentication mode="None" />` (`:24`) | A | `ApplicationConfigurationPreflight.ValidateAuthenticationMode` refuses only explicit `Windows` (`…/ApplicationConfigurationPreflight.cs:292-309`) |
| `<sessionState mode="InProc" customProvider="DefaultSessionProvider">` + `System.Web.Providers` provider row (`:66-70`) | A | `ValidateSessionStateMode` accepts InProc (`…:275-289`); the unresolvable provider type is never instantiated. Identity-application precedent |
| `<membership>`, `<profile>`, `<roleManager>` each with `<clear />` (`:36-59`) | A | The template's "membership disabled" comment blocks. **No provider is configured anywhere in this application**; roles come from Identity claims |
| `<customErrors mode="On" defaultRedirect="ErrorPage.aspx…">` + 404 row (`:21-23`) | A (unverified) | Surface exists; no compatibility claim. It will mask real bring-up failures behind a friendly page — turn it off while diagnosing |
| `<pages><namespaces>`/`<controls>` webopt registration (`:27-35`) | A | Default XDT rewrites the assembly name |
| `<httpModules>` with three Elmah rows (`:71-75`) | A (dead text) | `<validation validateIntegratedModeConfiguration="false" />` at `:80` waives the classic block; `ClassicSectionValidation.Validate` returns early (`src/Rehost.WebForms.Runtime/Compatibility/IisConfig/ClassicSectionValidation.cs:21-46`). Same as eShop |
| `<system.webServer><modules>` (`:77-79`) | **C, blocking** | Effective list: `<remove name="FormsAuthentication" />` (unhonored, as in the Identity application) plus `ErrorLog`, `ErrorMail`, `ErrorFilter` — all `Elmah.*`. A module row whose type will not load fails its URLs with the entry named (MH22a) |
| `<configSections>` `elmah` sectionGroup with four `Elmah.*` handler types (`:10-15`) and `<elmah><security/></elmah>` (`:107-112`) | A (unverified) | Section-handler types resolve lazily on Framework; whether the port's activation-time configuration load touches them is unmeasured. Cheapest answer is to drop the whole ELMAH surface in one XDT |
| `<location path="elmah.axd">` with `<httpHandlers>` and `<handlers>` (`:113-132`) | A (unverified) | The port builds folder handler lists by discovering folder `Web.config` files (`src/Rehost.WebForms.Runtime/Compatibility/IisConfig/IisFolderHandlers.cs:14-21`), not from root `<location>` blocks — the same reading the AjaxControlToolkit analysis recorded. Probably inert; nothing in the journey visits `elmah.axd` |
| `<connectionStrings>` `DefaultConnection` = `(LocalDb)\v11.0`, `WingtipToys` = `(LocalDB)\v11.0` + `AttachDbFilename=\|DataDirectory\|\wingtiptoys.mdf` (`:16-19`) | **D** | LocalDb is a Windows-only engine. `AttachDbFilename` is a LocalDb/Express user-instance feature with no container equivalent — the swap must become a plain `Initial Catalog=`, not just a server rename |
| `<entityFramework><defaultConnectionFactory type="…LocalDbConnectionFactory"><parameter value="v11.0" />` (`:97-102`) | **D** | Must go with the connection strings |
| `<runtime><assemblyBinding>` — Newtonsoft, WebGrease, EntityFramework (`:81-96`) | A (dropped) | Default XDT removes `<runtime>` wholesale |
| **No `<machineKey>`** | A | Auto-generated keys persist per application in a host-resolvable key file (ADR 0010), so logins survive restart. Worth an explicit `<machineKey>` only if the milestone adds multi-instance |

`Web.Debug.config` and `Web.Release.config` are the untouched template stubs;
the only live rule is `RemoveAttributes(debug)` in Release (`Web.Release.config:18`).

### Global.asax and App_Start wiring

`Global.asax.cs:17-32` runs, in order: `RouteConfig.RegisterRoutes` (Friendly
URLs with permanent auto-redirect), `BundleConfig.RegisterBundles`,
`Database.SetInitializer(new ProductDatabaseInitializer())`,
`RoleActions.AddUserAndRole()`, then two `MapPageRoute` registrations.

- **`AddUserAndRole` hits the database during `Application_Start`.**
  `Logic/RoleActions.cs:16-51` constructs `ApplicationDbContext` directly (not
  through the OWIN per-request factory), creates the `canEdit` role, creates
  `canEditUser@wingtiptoys.com` with password `Pa$$word1`, and adds the role.
  Both databases are therefore created and seeded on the first request, before
  any page runs. The Identity application's recorded hazard — reaching EF before
  `HostingEnvironment` initialises breaks the configuration system — does not
  apply, because `Application_Start` runs well after activation. **Unverified**
  that it holds under the port's activation ordering.
- `IdentityResult` return values are discarded at `:31,44`, so a second start
  (duplicate user) is silently tolerated. `FindByEmail` at `:48,50` then
  succeeds. Restart-safe by accident.
- `App_Start/Startup.Auth.cs:20-47` is the stock Identity 2.x pipeline the
  Milestone 1 Identity application already proves: per-OWIN-context DbContext /
  UserManager / SignInManager, cookie authentication with a 30-minute security
  stamp revalidation, external/two-factor/remember-browser cookies.
- `Startup.Auth.cs:62-66` enables **Google** external authentication with the
  placeholder client id `000000000000.apps.googleusercontent.com`. It is not
  commented out like the other three, so `OpenAuthProviders.ascx` renders a
  Google button on Login and Register (`OpenAuthProviders.ascx.cs:40`). Clicking
  it issues an OWIN challenge to Google and fails there — a live network
  dependency on the registration page, though not on the registration path.
- `App_Start/BundleConfig.cs:41-47` registers a `respond` script definition
  itself; `jquery` and `bootstrap` are not registered.
- `App_Start/IdentityConfig.cs:13-29` — `EmailService` and `SmsService` are
  `Task.FromResult(0)` no-ops. **There is no email sender to substitute**, and
  account confirmation is commented out at `Account/Register.aspx.cs:22-24`.

## Stateful surfaces this milestone exists to drive

This is the reason Wingtip Toys is Milestone 3, so it deserves its own table
rather than a line in the inventory.

| Surface | Where | Why it is new |
| --- | --- | --- |
| **Session-keyed, database-backed cart** | `Logic/ShoppingCartActions.cs:59-75` | The cart id lives in `Session["CartId"]` but the cart *rows* live in SQL. Every page render reads it: `Site.Master.cs:82-86` calls `GetCount()` in `Page_PreRender`. Session loss and database loss are different failures with different symptoms |
| **Anonymous → authenticated cart migration** | `Logic/ShoppingCartActions.cs:216-225`, called from `Account/Login.aspx.cs:41-43` and `Register.aspx.cs:28` | On sign-in the GUID cart id is rewritten to the user name in the database *and* in session. Exercises session mutation across an OWIN sign-in in the same request |
| **Identity 2.x register/login/logoff over OWIN cookies** | `Account/*`, `Site.Master:61-76` | Already proven by `WebFormsIdentityApplication`; here it gates the rest of the journey |
| **URL authorization by role, from claims not a provider** | `Admin/Web.config:4-7` (`allow roles="canEdit"`), `Site.Master.cs:74` | Map calls managed URL authorization Partial. The role claim is written by Identity into the cookie; no `RoleProvider`, no `RoleManagerModule`. The port has never run role-based `<authorization>` against an Identity-claims principal — **unverified** |
| **URL authorization by anonymity on a folder** | `Checkout/Web.config:4-6`, `Account/Web.config:4-10` (`<location>` inside a folder file) | The `Account/` file nests `<location path="Manage.aspx">` inside a folder `Web.config` — a shape neither prior application uses |
| **Model binding writes** | `ShoppingCart.aspx.cs:45-71`, `Admin/AdminPage.aspx.cs:28-114` | `SelectMethod` on `GridView`/`FormView`/`DetailsView`/`DropDownList`, plus manual `ExtractValuesFromCell` |
| **EF6 Code First against a real server** | `Models/ProductContext.cs`, `Models/ProductDatabaseInitializer.cs:6-12` | `DropCreateDatabaseIfModelChanges` seeds 5 categories and 16 products. Two contexts, two databases, one server |
| **Multi-step checkout carried entirely in session** | `Checkout/CheckoutStart` → `CheckoutReview` → `CheckoutComplete` | `payment_amt`, `token`, `payerId`, `currentOrderId`, `userCheckoutCompleted` — five session keys crossing four requests and an external redirect |
| **Order write with a deliberate mismatch guard** | `Checkout/CheckoutReview.aspx.cs:46-56, 59-84` | Compares the session amount against the PayPal amount, then writes `Order` + one `OrderDetail` per line with a `SaveChanges` per row |
| **Membership / role / profile providers** | — | **None.** All three sections are `<clear />`ed (`Web.config:36-59`). The SQL-provider journey the backlog still lists as open is *not* exercised by this application |

Two latent defects in the frozen source are worth knowing before they are
blamed on the port. `Checkout/CheckoutComplete.aspx.cs:47-49` compares
`Session["currentOrderId"]` (an `int`) to `string.Empty` — always true — and
then reads `Session["currentOrderID"]` with different casing, which is a
different key and returns null, so `Convert.ToInt32(null)` yields 0 and the
order lookup uses order 0. And `Logic/ShoppingCartActions.cs:99-107`
(`GetCart`) returns a cart it has already disposed. Neither is reached by the
happy path as written, but both will look like port bugs if hit.

## External-service boundaries

| Boundary | Evidence | Disposition |
| --- | --- | --- |
| **PayPal Express Checkout (NVP API)** | `Logic/PayPalFunctions.cs:26-27` sandbox endpoint `https://api-3t.sandbox.paypal.com/nvp`, host `www.sandbox.paypal.com`; `:187-209` a synchronous `HttpWebRequest` with a 15 s timeout | Network dependency, not port work. **It cannot work as shipped**: `:36-38` carry the literal placeholders `<Your API Username>`, `<Your API Password>`, `<Your Signature>`, which the tutorial tells the reader to replace with their own sandbox credentials. `:62-63` also hard-code `https://localhost:44300/Checkout/…` as the PayPal return and cancel URLs, which no rehosted host will be listening on |
| Google external login | `App_Start/Startup.Auth.cs:62-66` | Live OAuth with a placeholder client id. Renders a button on Login/Register; the challenge fails at Google |
| Email / SMS | `App_Start/IdentityConfig.cs:13-29` | No-op stubs. Nothing to substitute; account confirmation and password reset are commented out in the pages |
| ELMAH error mail | `Web.config:73` `Elmah.ErrorMailModule` | No `<errorMail>` element is configured, so it is inert even on Framework |
| `ShoppingCart.aspx:43` checkout button image | `https://www.paypal.com/en_US/i/btn/btn_xpressCheckout.gif` | An external `<img>`. The page renders without it; a smoke script must not assert on it |

The practical consequence: the register→browse→cart journey is fully local, and
**checkout terminates at `CheckoutStart.aspx`**, which redirects to
`CheckoutError.aspx` when `ShortcutExpressCheckout` fails. That is a *defined*
outcome and it exercises session read, PayPal call, and redirect — it just never
reaches `CheckoutReview`, the `Order` write, or `EmptyCart`. Reaching those
needs either real sandbox credentials or a local stand-in. This is an open
decision, not port work.

## Windows-only API scan

A scan of every `*.cs`, `*.aspx`, `*.ascx`, `*.master`, `*.asax` and `*.config`
in the application tree for `DllImport`, `Microsoft.Win32`, `RegistryKey`,
`EventLog`, `ProtectedData`, `WindowsIdentity`, `WindowsPrincipal`, `ComImport`,
`Marshal.`, `System.Management`, `Process.Start`, `System.Drawing`, `Bitmap`,
`PerformanceCounter`, `ServiceController` and `Impersonat` returns **zero hits**.
There is no `App_Code` folder, no P/Invoke, no COM, no registry, no DPAPI, no
WMI, no GDI+.

A separate backslash-literal scan (the check both prior analyses missed once)
returns three hits, none of them a path defect:

| Hit | Evidence | Effect |
| --- | --- | --- |
| `(LocalDb)\v11.0` and `\|DataDirectory\|\wingtiptoys.mdf` | `Web.config:17-18` | Windows-only engine; replaced by the connection-string swap regardless |
| `url[1] != '\\'` in the local-URL check | `Models/IdentityModels.cs:92` | Correct: it is rejecting a protocol-relative URL, not building a path |
| `\.` in the email regex | `Models/Order.cs:51` | Regex escape |

Everything else builds paths correctly: `Logic/ExceptionUtility.cs:21` uses
`"App_Data/ErrorLog.txt"` with a forward slash through `Server.MapPath`, and
`Admin/AdminPage.aspx.cs:31,50-52` concatenates
`Server.MapPath("~/Catalog/Images/")` with `"Thumbs/" + FileName`. Whether
`MapPath` on a trailing-slash virtual path returns a trailing separator — and
therefore whether that concatenation produces a valid path off Windows — is
**unverified** and is worth one check during bring-up rather than an assumption.

Two casing notes for case-sensitive filesystems:

| Hazard | Evidence | Effect |
| --- | --- | --- |
| `Bundle.config` on disk vs `~/bundle.config` in the manifest reader | `Bundle.config`; `src/System.Web.Optimization.ReferenceSource/BundleManifest.cs:18` | Bundle resolution defaults to `HostingEnvironment.VirtualPathProvider`, whose MapPath exits through `CanonicalCasePath` (P57), which should fold the miss. If it does not, the site renders with **no stylesheet at all** — `~/Content/css` exists only in this file |
| `CodeBehind="Site.master.cs"` / `"Site.Mobile.master.cs"` vs `Site.Master.cs` on disk | `Site.Master:1`, `Site.Mobile.Master:1` | Compile-time metadata in a WAP; the runtime resolves `Inherits`. Inert, exactly as in eShop |
| `~/Scripts/WebForms/MsAjax/*` in `BundleConfig.cs:27-30` vs `Scripts/WebForms/MSAjax/` on disk | `App_Start/BundleConfig.cs:27-30` | The same P57-covered mismatch eShop and the plain template carry. Higher stakes here because `EnableOptimizations = true` |

`Catalog/Images/Thumbs.db` and `Catalog/Images/Thumbs/Thumbs.db` are checked in
as `<Content>` (`csproj:227-228`). They are Windows Explorer artifacts, harmless
as static files, and not hidden by name.

## Verdict

**Expected to run with no library recompile.** This is the first application
whose entire third-party closure is already solved: the OWIN host is
`Rehost.WebForms.Owin.Host.SystemWeb`, Identity 2.x / EF6 / Owin.Security come
from nuget.org unchanged, Optimization and FriendlyUrls have `Rehost.*`
packages, and the one remaining `System.Web`-binding assembly (ELMAH) is
unreferenced by application code and can be dropped rather than ported. There is
no Autofac, no Application Insights, no telemetry stack, no `System.Design`
closure, no `App_Code`, no Web Site project model, no build customisation.

**Needs configuration work.** Four XDT concerns: drop the ELMAH surface, swap
both connection strings off LocalDb, remove `LocalDbConnectionFactory`, and
decide whether to register the `jquery`/`bootstrap` script mappings via a shim
(the eShop pattern) or to set `ValidationSettings:UnobtrusiveValidationMode` to
`None` by XDT (the AjaxControlToolkit pattern). The eShop shim is the better fit
here, because both scripts are genuinely present in `Scripts/`.

**Needs host work the prior applications did not.** A real SQL Server, and with
it the `System.Data.SqlClient` factory question the Identity application solved
for SQLite in host code. Two databases, both created by EF at first request.

**Windows-only, needing a stated boundary.** LocalDb only. Nothing else in the
tree is platform-bound.

**New port surface, small but real.** Production-mode Optimization on first
render; role-based `<authorization>` against an Identity-claims principal;
`GridView`/`FormView`/`DetailsView` model binding; `[RouteData]`/`[QueryString]`
value providers; `customErrors` end to end.

### Gap list, ordered by likelihood of blocking the register→browse→cart→checkout journey

1. **The `jquery` `ScriptResourceMapping` is unregistered, and at
   `targetFramework="4.5.2"` that is fatal.**
   `ValidationSettings.UnobtrusiveValidationMode` resolves to `WebForms`
   (`src/System.Web.ReferenceSource/UI/ValidationSettings.cs:20`), so every
   validator calls `ClientScriptManager.EnsureJqueryRegistered`, which **throws
   `InvalidOperationException` when no `jquery` definition exists**
   (`src/System.Web.ReferenceSource/UI/ClientScriptManager.cs:175-182`, reached
   from `BaseValidator.cs:645-646`). `Account/Register.aspx` and
   `Account/Login.aspx` — the journey's first two pages — carry
   `RequiredFieldValidator`s. The definition came from the
   `AspNet.ScriptManager.jQuery` package, which the port replaces with
   `Rehost.WebForms.ScriptManager.Bundles` (names only). Fix: an
   `apps/WingtipToys/WingtipToys.App/PreApplicationStartCode.cs` in the shape of
   `apps/eShopLegacyWebForms/eShopLegacyWebForms.App/PreApplicationStartCode.cs:16-30`,
   pointing at `~/Scripts/jquery-1.10.2.min.js` and `~/Scripts/bootstrap.min.js`.
2. **Three ELMAH modules are registered in `<system.webServer><modules>.**
   `Elmah.dll` binds Microsoft's strong-named `System.Web` and has no portable
   build. A module row whose type will not load fails its URLs with the entry
   named (MH22a), so this blocks every request until the rows are removed. This
   is the eShop Application-Insights drop repeated. Also decide whether to drop
   the `elmah` `<configSections>` group, the `<elmah>` element and the
   `<location path="elmah.axd">` block in the same transform — the alternative
   is proving they are parsed lazily, which is currently **unverified**.
3. **LocalDb, twice, one of them with `AttachDbFilename`.** `Web.config:16-19`
   plus `LocalDbConnectionFactory` at `:97-102`. `Data Source=(LocalDb)\v11.0`
   is a Windows-only engine and `AttachDbFilename=|DataDirectory|\wingtiptoys.mdf`
   has no container equivalent, so `WingtipToys` must become a named catalog on
   a real server, not a renamed data source. Nothing renders before this is
   fixed: `Site.Master.cs:82-86` queries the cart on every page render and
   `Site.Master.cs:89-93` queries categories.
4. **`Application_Start` creates and seeds two databases before the first page.**
   `Global.asax.cs:24,27-28` — `Database.SetInitializer` plus
   `RoleActions.AddUserAndRole()`. A failure here is an application-start
   failure, not a page failure, and `customErrors mode="On"` will present it as
   a friendly page. `DropCreateDatabaseIfModelChanges` needs DDL permission on
   the target server.
5. **Production Optimization runs on the first render of every page.**
   `App_Start/BundleConfig.cs:39` sets `EnableOptimizations = true`
   unconditionally, overriding `debug="true"`. `Site.Master:12,14` render
   `~/bundles/modernizr` and `~/Content/css` on every page, so WebGrease
   combination and minification are on the critical path. The backlog records
   this exact path as unvalidated and calls it higher priority than it reads;
   the AjaxControlToolkit site only proved the seam does not crash over an empty
   bundle. Here the bundles are real: `modernizr-2.6.2.js`, `bootstrap.css` and
   `Site.css`. Compounding it, `~/Content/css` exists **only** in `Bundle.config`
   and reaches the runtime through `BundleTable.EnsureBundleSetup`
   (`src/System.Web.Optimization.ReferenceSource/BundleTable.cs:57-64`) reading
   `~/bundle.config` — a path no prior application exercised.
6. **Role-based `<authorization>` against an Identity-claims principal.**
   `Admin/Web.config:4-7` allows `roles="canEdit"` with no `RoleProvider`
   configured anywhere; `Site.Master.cs:74` calls `IsInRole` on every render.
   Map: URL authorization Partial, forms auth/roles Partial with the note that
   "general claims payloads" remain open. Blocks the admin journey only, but the
   `IsInRole` call is on every page.
7. **Model binding on `GridView`, `FormView`, `DetailsView` and `DropDownList`.**
   `ShoppingCart.aspx:4-6` (the cart itself), `ProductDetails.aspx:4`,
   `Admin/AdminPage.aspx:11-12,56-57`, `Account/ManageLogins.aspx:13-14`
   (`DeleteMethod`). The map's `ItemType` claim covers `ListView`/`DataPager`;
   these controls are Unassessed. `ShoppingCart.aspx.cs:81`
   (`ExtractValuesFromCell`) is the cart Update postback.
8. **Two `MapPageRoute` routes plus `[RouteData]` model binding, alongside
   active Friendly URLs.** `Global.asax.cs:36-45` and
   `App_Start/RouteConfig.cs:15` both register into `RouteTable.Routes`, with
   Friendly URLs registered *first*. `ProductDetails.aspx.cs:20-21` binds both
   `[QueryString("ProductID")]` and `[RouteData] productName`. Neither prior
   application combines the two routing systems.
9. **`customErrors mode="On"` masking everything above.** `Web.config:21-23`.
   No compatibility claim covers `customErrors` end to end, and during bring-up
   it converts every diagnosable failure into `ErrorPage.aspx`. Turn it off
   first; assess it deliberately afterwards.
10. **PayPal terminates the checkout journey.** With placeholder credentials
    (`Logic/PayPalFunctions.cs:36-38`) and `localhost:44300` return URLs
    (`:62-63`), `CheckoutStart.aspx` redirects to `CheckoutError.aspx`. The
    order write, `EmptyCart`, and `CheckoutComplete` are unreachable without a
    decision. Not port work — but it decides how much of the milestone's stated
    "checkout" surface actually gets driven.
11. **Font and static MIME coverage** for `fonts/*.woff|eot|svg|ttf`, and the
    `MsAjax`/`MSAjax` casing mismatch under production bundling. Lowest risk;
    prior applications carry the same files.

### Effort comparison against the eShop bring-up

eShop cost: 2 XDT module drops, 1 library recompile (`Autofac.Integration.Web`),
1 baseline-config fix (expression builders), 1 script-mapping shim.

| Axis | eShop | Wingtip Toys |
| --- | --- | --- |
| XDT edits | 2 module drops | ~4: ELMAH surface, 2 connection strings, `defaultConnectionFactory` |
| Library recompiles | 1 (`Autofac.Integration.Web`, ~10 files) | **0** — the OWIN host already landed; ELMAH is dropped, not ported |
| Script-mapping shim | 1 (`jquery`, `bootstrap`) | 1, identical — and here it is provably blocking, not speculative |
| Baseline-config fix | 1 (`expressionBuilders`) | none anticipated |
| Host code beyond the ~30-line Kestrel host | none | SQL Server connection + `SqlClient` factory registration (**unverified** whether `System.Data.SqlClient` needs the explicit `DbProviderFactories.RegisterFactory` the Identity application needed for SQLite) |
| New port surface to assess | routing, model binding | production Optimization, `bundle.config` manifest, claims-role authorization, 4 more model-bound controls, `customErrors` |
| Windows-only boundary | LocalDb + AI perf counters, both unreached in the journey | LocalDb only — but **reached on every page**, so it must be solved before anything renders |
| External service | none | PayPal sandbox, unusable as shipped |

A fair estimate is **roughly eShop's effort, redistributed**. The library side is
cheaper — nothing to recompile, which was eShop's single largest item and the
AjaxControlToolkit's dominant cost. The configuration side is slightly heavier.
The genuinely new work is the database: a real SQL Server on three platforms,
two EF contexts creating their own schemas at first request, with the roadmap
already naming it as this milestone's scope. Production Optimization is the one
item that could move the estimate materially, because it is unvalidated, it is
on the first render of every page, and it is the port's own recorded open
question rather than an application quirk.

### Open decisions for the user

1. **Source authenticity.** The tree is Microsoft-authored and Microsoft-hosted,
   but recovered from an Internet Archive capture rather than a live Microsoft
   URL, and no publisher digest survives to verify it against. Accepting it
   freezes an archive artifact as milestone input. The alternative candidates
   are all individual GitHub forks with weaker provenance, so there is no better
   option — this is an accept-or-reconsider-the-milestone decision, not a
   choice between sources.
2. **PayPal.** Three options, in ascending cost: (a) record the boundary and end
   the journey at `CheckoutError.aspx`, which still exercises session, the
   outbound call and the redirect; (b) supply real sandbox credentials plus a
   reachable return URL, making the journey network-dependent and non-hermetic;
   (c) stand up a local NVP responder so `CheckoutReview` → `CheckoutComplete`
   run offline, which drives the order write, the `OrderDetail` loop and
   `EmptyCart` — the richest stateful surface in the application, and the one
   the milestone exists for. (c) is the only option that actually tests
   checkout, but it is the only one that adds a fixture.
3. **ELMAH.** Drop it by XDT (no application code touches it) or recompile
   `elmah.corelibrary` under Rehost identity. Dropping is right on cost; keeping
   it would preserve the tutorial's final chapter as written and prove one more
   third-party `System.Web` consumer. Related: whether to drop only the three
   module rows or the whole ELMAH configuration surface, which turns on whether
   the port resolves `<configSections>` handler types lazily — currently
   unverified.
4. **SQL Server topology.** One container serving both `aspnet-WingtipToys` and
   `WingtipToys`, or one database for both contexts. The tutorial ships two
   connection strings; collapsing them is an app-visible change beyond the
   accepted connection-string swap.
5. **The Google external-login button.** `Startup.Auth.cs:62-66` is uncommented
   with a placeholder client id, unlike the other three providers. Leaving it
   renders a button on Register and Login that fails at Google; commenting it
   out is an application-source change the frozen-tree rule forbids, so it would
   have to be an XDT or a recorded boundary.
6. **`customErrors mode="On"`.** Leave it as authored and diagnose through
   `App_Data/ErrorLog.txt`, or XDT it off for bring-up and restore it before the
   smoke. Prior applications had nothing equivalent.

## Sources

- Application: `/Users/vm/repos/WingtipToys/C#/WingtipToys` — MSDN Code Gallery
  `Getting-Started-with-221c01f5`, file version 11, retrieved 2026-08-29 from
  Internet Archive snapshot `20170710030442` (SHA-256
  `3b8760a509118992d2b8aedfb95422160262d2c8b342e2a926a3c754a4d67468`).
  Files read: `WingtipToys.csproj`, `Web.config`, `Web.Debug.config`,
  `Web.Release.config`, `Bundle.config`, `packages.config`, `Global.asax*`,
  `Startup.cs`, `App_Start/*.cs`, `Logic/*.cs`, `Models/*.cs`, `Site.Master*`,
  `Site.Mobile.Master*`, `ViewSwitcher.ascx*`, `Default.aspx*`,
  `ProductList.aspx*`, `ProductDetails.aspx*`, `ShoppingCart.aspx*`,
  `AddToCart.aspx*`, `ErrorPage.aspx*`, `Admin/*`, `Checkout/*`, `Account/*`,
  `../WingtipToys.sln`, `description.html`
- Support claims: [compatibility map](../compatibility.md)
- Process: [bringing up an application](../bringing-up-an-application.md)
- Unresolved work referenced: [backlog](../backlog.md)
- Precedents: [eShop portability](eshoplegacywebforms-portability.md),
  [AjaxControlToolkit sample site portability](ajaxcontroltoolkit-samplesite-portability.md),
  [Identity application findings](webforms-identity-application-gaps.md),
  [`apps/WebFormsIdentityApplication/README.md`](../../apps/WebFormsIdentityApplication/README.md)
